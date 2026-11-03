using System.Security.Cryptography;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using EByahe.Application.Exceptions;
using EByahe.Application.Interfaces;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Domain.Interfaces;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Services;

public sealed class PartnerApplicationService(IPartnerApplicationRepository applications, IDocumentStorage storage,
  TimeProvider clock, ILogger<PartnerApplicationService> logger)
  : IPartnerApplicationService
{
  public async Task<CustomerPartnerApplicationPage> ListAsync(string userId, int page, int pageSize,
    CancellationToken cancellationToken = default)
  {
    var result = await applications.ListForUserAsync(userId, page, pageSize, cancellationToken);
    return new CustomerPartnerApplicationPage(result.Items.Select(application => new CustomerPartnerApplicationSummary(
      application.Id, application.TrackingId, application.BusinessName, application.BusinessCategory,
      application.Status, application.SubmittedAtUtc)).ToArray(), result.TotalCount, page, pageSize);
  }

  public async Task<CustomerPartnerApplicationDetails?> GetAsync(Guid id, string userId, CancellationToken cancellationToken = default)
  {
    var application = await applications.GetForUserAsync(id, userId, cancellationToken);
    if (application is null) return null;
    var digits = new string(application.AccountNumber.Where(char.IsDigit).ToArray());
    var maskedNumber = "•••• " + (digits.Length > 4 ? digits[^4..] : new string('•', digits.Length));
    return new CustomerPartnerApplicationDetails(application.Id, application.TrackingId, application.Status,
      application.SubmittedAtUtc, application.BusinessCategory, application.BusinessName, application.TradeName,
      application.EntityType, application.Tin, application.Address, application.RepresentativeName,
      application.RepresentativeEmail, application.RepresentativeMobile, application.BankName, maskedNumber,
      application.AccountHolder, application.SettlementFrequency, application.Documents.OrderBy(document => document.DocumentType)
        .Select(document => new CustomerPartnerApplicationDocument(document.Id, document.DocumentType, document.FileName,
          document.ContentType, document.SizeBytes, document.UploadedAtUtc)).ToArray());
  }

  public async Task<CustomerPartnerDocumentContent?> GetDocumentAsync(Guid id, Guid documentId, string userId,
    CancellationToken cancellationToken = default)
  {
    var application = await applications.GetForUserAsync(id, userId, cancellationToken);
    var document = application?.Documents.SingleOrDefault(item => item.Id == documentId);
    if (document is null) return null;
    var content = await storage.ReadAsync(new StoredDocument(document.StorageBucket, document.StorageKey), cancellationToken);
    if (content.LongLength != document.SizeBytes || !string.Equals(Convert.ToHexString(SHA256.HashData(content)),
        document.Sha256, StringComparison.OrdinalIgnoreCase))
      throw new EByahe.Domain.Exceptions.DocumentStorageException("The stored document could not be verified.");
    return new CustomerPartnerDocumentContent(content, document.ContentType, document.FileName);
  }

  public async Task<PartnerApplicationResponse> SubmitAsync(CreatePartnerApplicationRequest request, string createdBy,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(createdBy) || createdBy.Length > 255 || createdBy != createdBy.Trim())
      throw new ArgumentException("A valid authenticated user ID is required.", nameof(createdBy));
    var now = clock.GetUtcNow();
    var id = Guid.NewGuid();
    var corporate = request.Corporate;
    var payout = request.Payout;
    var application = new PartnerApplication
    {
      Id = id, TrackingId = $"APP-{id:N}".ToUpperInvariant(), SubmittedAtUtc = now, CreatedBy = createdBy,
      BusinessCategory = request.BusinessCategory, BusinessName = corporate.BusinessName.Trim(),
      TradeName = corporate.TradeName.Trim(), EntityType = corporate.EntityType, Tin = corporate.Tin.Trim(),
      Address = corporate.Address.Trim(), RepresentativeName = corporate.RepresentativeName.Trim(),
      RepresentativeEmail = corporate.RepresentativeEmail.Trim(), RepresentativeMobile = corporate.RepresentativeMobile.Trim(),
      BankName = payout.BankName.Trim(), AccountNumber = payout.AccountNumber.Trim(),
      AccountHolder = payout.AccountHolder.Trim(), SettlementFrequency = payout.SettlementFrequency
    };
    var validated = new List<(PartnerApplicationDocument Document, byte[] Content)>();
    foreach (var (kind, file) in new (string, IFormFile?)[]
    {
      ("registration", request.Documents.Registration), ("bir", request.Documents.Bir),
      ("permit", request.Documents.Permit), ("accreditation", request.Documents.Accreditation),
      ("bankAccountProof", request.Documents.BankAccountProof)
    })
      validated.Add(await ReadDocumentAsync(id, kind, file, now, cancellationToken));

    var uploaded = new List<StoredDocument>();
    var saveAttempted = false;
    try
    {
      // Validate every upload before writing any object; only metadata is attached to the EF aggregate.
      foreach (var (document, content) in validated)
      {
        document.StorageBucket = storage.Bucket;
        document.StorageKey = DocumentStorageKey.Create(id, document.Id, document.Sha256);
        uploaded.Add(new StoredDocument(document.StorageBucket, document.StorageKey));
        await storage.PutAsync(document.StorageKey, content, document.ContentType, document.Sha256, cancellationToken);
        application.Documents.Add(document);
      }
      saveAttempted = true;
      await applications.AddAsync(application, cancellationToken);
    }
    catch
    {
      // A lost database commit acknowledgement must not cause deletion of files referenced by a committed application.
      var canClean = !saveAttempted;
      if (saveAttempted)
      {
        try { canClean = !await applications.ExistsAsync(id, CancellationToken.None); }
        catch { logger.LogWarning("Could not confirm the application commit outcome; uploaded objects were retained for reconciliation."); }
      }
      if (canClean)
      {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var document in uploaded)
          try { await storage.DeleteAsync(document, cleanup.Token); }
          catch { logger.LogWarning("An uncommitted document upload could not be cleaned up; reconcile the application objects."); }
      }
      throw;
    }
    return new PartnerApplicationResponse(application.Id, application.TrackingId, application.Status, now);
  }

  private static async Task<(PartnerApplicationDocument Document, byte[] Content)> ReadDocumentAsync(Guid applicationId, string kind,
    IFormFile? file, DateTimeOffset now, CancellationToken cancellationToken)
  {
    var field = $"documents.{kind}";
    if (file is null) throw new PartnerApplicationValidationException(field, "Attach this required document.");
    if (file.Length is <= 0 or > PartnerApplicationDocument.MaxFileSize)
      throw new PartnerApplicationValidationException(field, "Choose a non-empty file no larger than 10 MB.");
    var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
    if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name.Any(char.IsControl))
      throw new PartnerApplicationValidationException(field, "Choose a file with a valid name of at most 255 characters.");
    var contentType = PartnerDocumentFormat.ContentType(name);
    if (contentType is null || (!string.IsNullOrWhiteSpace(file.ContentType)
      && file.ContentType != "application/octet-stream" && !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase)))
      throw new PartnerApplicationValidationException(field, "Choose an image (JPG, PNG, GIF, WebP, BMP), PDF, or Word (DOC, DOCX) file with a matching content type.");

    await using var source = file.OpenReadStream();
    using var buffer = new MemoryStream((int)file.Length);
    // Limit the stream as well as the reported length; never trust client metadata.
    var chunk = new byte[81920];
    int count;
    while ((count = await source.ReadAsync(chunk, cancellationToken)) != 0)
    {
      if (buffer.Length + count > PartnerApplicationDocument.MaxFileSize)
        throw new PartnerApplicationValidationException(field, "Choose a file no larger than 10 MB.");
      await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
    }
    var content = buffer.ToArray();
    if (content.Length != file.Length || !PartnerDocumentFormat.Matches(content, contentType))
      throw new PartnerApplicationValidationException(field, "The file contents do not match the selected image, PDF, or Word format.");
    return (new PartnerApplicationDocument
    {
      Id = Guid.NewGuid(), PartnerApplicationId = applicationId, DocumentType = kind, FileName = name,
      ContentType = contentType, SizeBytes = content.LongLength,
      Sha256 = Convert.ToHexString(SHA256.HashData(content)), UploadedAtUtc = now
    }, content);
  }

}
