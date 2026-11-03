using System.Text;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Exceptions;
using EByahe.Application.Services;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EByahe.Tests;

public sealed class PartnerDocumentStorageTests
{
  private static readonly byte[] Pdf = "%PDF-1.7\nStorage test\n%%EOF"u8.ToArray();

  private static CreatePartnerApplicationRequest Request()
  {
    static IFormFile File(string kind) => new FormFile(new MemoryStream(Pdf), 0, Pdf.Length, kind, kind + ".pdf")
      { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    return new CreatePartnerApplicationRequest
    {
      BusinessCategory = "hotel",
      Corporate = new PartnerCorporateRequest { BusinessName = "Storage Test", TradeName = "Test", EntityType = "corporation", Tin = "123", Address = "Manila", RepresentativeName = "Test", RepresentativeEmail = "test@example.com", RepresentativeMobile = "+639171234567" },
      Payout = new PartnerPayoutRequest { BankName = "Test Bank", AccountNumber = "123", AccountHolder = "Test", SettlementFrequency = "weekly" },
      Documents = new PartnerDocumentsRequest { Registration = File("registration"), Bir = File("bir"), Permit = File("permit"), Accreditation = File("accreditation"), BankAccountProof = File("bankAccountProof") }
    };
  }

  private sealed class Repository : IPartnerApplicationRepository
  {
    public Task<(IReadOnlyList<PartnerApplication> Items, int TotalCount)> ListForUserAsync(string userId, int page,
      int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<PartnerApplication?> GetForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
    public PartnerApplication? Application { get; private set; }
    public bool FailSave { get; init; }
    public bool Committed { get; init; }
    public bool CannotCheckCommit { get; init; }
    public Task AddAsync(PartnerApplication application, CancellationToken cancellationToken = default)
    {
      if (FailSave) throw new IOException("Simulated database failure.");
      Application = application;
      return Task.CompletedTask;
    }
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
      CannotCheckCommit ? throw new IOException("Unknown commit outcome.") : Task.FromResult(Committed);
  }

  private static PartnerApplicationService Service(Repository repository, MemoryDocumentStorage storage) =>
    new(repository, storage, TimeProvider.System, NullLogger<PartnerApplicationService>.Instance);

  [Fact]
  public async Task SavesFilesInStorageAndOnlyReferencesInTheAggregate()
  {
    var repository = new Repository(); var storage = new MemoryDocumentStorage();
    await Service(repository, storage).SubmitAsync(Request(), "authenticated-user");
    Assert.Equal(5, storage.Objects.Count);
    Assert.All(repository.Application!.Documents, document =>
    {
      Assert.Equal(storage.Bucket, document.StorageBucket);
      Assert.StartsWith($"ebyahe/partner-applications/{repository.Application.Id:N}/{document.Id:N}/", document.StorageKey);
      Assert.Equal(Pdf, storage.Objects[document.StorageKey]);
    });
    Assert.Null(typeof(PartnerApplicationDocument).GetProperty("Content"));
  }

  [Fact]
  public async Task ValidatesAllFiveDocumentsBeforeUploadingAnything()
  {
    var request = Request();
    request.Documents.BankAccountProof = new FormFile(new MemoryStream(Pdf), 0, Pdf.Length, "bankAccountProof", "bad.exe");
    var repository = new Repository(); var storage = new MemoryDocumentStorage();
    await Assert.ThrowsAsync<PartnerApplicationValidationException>(() => Service(repository, storage).SubmitAsync(request, "user"));
    Assert.Equal(0, storage.PutCalls); Assert.Null(repository.Application);
  }

  [Fact]
  public async Task PartialUploadFailureCleansAllObjectsIncludingAnUnacknowledgedPut()
  {
    var repository = new Repository(); var storage = new MemoryDocumentStorage { FailAtPut = 2 };
    await Assert.ThrowsAsync<DocumentStorageException>(() => Service(repository, storage).SubmitAsync(Request(), "user"));
    Assert.Empty(storage.Objects); Assert.Null(repository.Application);
  }

  public static IEnumerable<object[]> AllowedUploads() => PartnerUploadTestFiles.Allowed();

  [Theory]
  [MemberData(nameof(AllowedUploads))]
  public async Task BankProofAcceptsOnlySupportedDocumentFormatsAndStoresTheirMetadata(string name, string type, byte[] bytes)
  {
    var request = Request();
    request.Documents.BankAccountProof = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "bankAccountProof", name)
      { Headers = new HeaderDictionary(), ContentType = type };
    var repository = new Repository(); var storage = new MemoryDocumentStorage();
    await Service(repository, storage).SubmitAsync(request, "customer");
    var proof = Assert.Single(repository.Application!.Documents, document => document.DocumentType == "bankAccountProof");
    Assert.Equal(type, proof.ContentType);
    Assert.Equal(name, proof.FileName);
    Assert.Equal(bytes, storage.Objects[proof.StorageKey]);
    Assert.Equal(5, storage.Objects.Count);
  }

  [Fact]
  public async Task MissingBankProofIsRejectedBeforeAnyUploads()
  {
    var request = Request(); request.Documents.BankAccountProof = null;
    var repository = new Repository(); var storage = new MemoryDocumentStorage();
    var error = await Assert.ThrowsAsync<PartnerApplicationValidationException>(() => Service(repository, storage).SubmitAsync(request, "customer"));
    Assert.Contains("documents.bankAccountProof", error.Errors.Keys);
    Assert.Equal(0, storage.PutCalls);
    Assert.Null(repository.Application);
  }

  [Fact]
  public async Task RenamedArchivesOtherOfficeFilesAndMismatchedTypesAreRejectedBeforeUpload()
  {
    var docxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    foreach (var (name, type, bytes) in new[]
    {
      ("fake.docx", docxType, Pdf),
      ("spreadsheet.docx", docxType, PartnerUploadTestFiles.Docx(word: false)),
      ("broken.docx", docxType, PartnerUploadTestFiles.Docx(malformedTypes: true)),
      ("spreadsheet.doc", "application/msword", PartnerUploadTestFiles.Doc("Workbook")),
      ("wrong-type.docx", "application/pdf", PartnerUploadTestFiles.Docx()),
      ("script.svg", "image/svg+xml", "<svg/>"u8.ToArray()),
      ("program.exe", "application/octet-stream", Pdf)
    })
    {
      var request = Request();
      request.Documents.BankAccountProof = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "bankAccountProof", name)
        { Headers = new HeaderDictionary(), ContentType = type };
      var repository = new Repository(); var storage = new MemoryDocumentStorage();
      await Assert.ThrowsAsync<PartnerApplicationValidationException>(() => Service(repository, storage).SubmitAsync(request, "customer"));
      Assert.Equal(0, storage.PutCalls);
      Assert.Null(repository.Application);
    }
  }

  [Fact]
  public async Task ConfirmedDatabaseFailureCleansUncommittedFiles()
  {
    var repository = new Repository { FailSave = true }; var storage = new MemoryDocumentStorage();
    await Assert.ThrowsAsync<IOException>(() => Service(repository, storage).SubmitAsync(Request(), "user"));
    Assert.Empty(storage.Objects);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task DoesNotDeleteFilesWhenCommitSucceededOrItsOutcomeCannotBeConfirmed(bool committed)
  {
    var repository = new Repository { FailSave = true, Committed = committed, CannotCheckCommit = !committed };
    var storage = new MemoryDocumentStorage();
    await Assert.ThrowsAsync<IOException>(() => Service(repository, storage).SubmitAsync(Request(), "user"));
    Assert.Equal(5, storage.Objects.Count);
  }

  [Theory]
  [InlineData("appsettings.Development.json", "http://localhost:18333", "get-it-done-dev")]
  [InlineData("appsettings.UAT.json", "https://storage.getitdone.com.ph", "get-it-done-uat")]
  [InlineData("appsettings.Production.json", "https://storage.getitdone.com.ph", "get-it-done-production")]
  public void EnvironmentSettingsChooseTheCorrectStorage(string file, string endpoint, string bucket)
  {
    var config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
      .AddJsonFile("appsettings.json").AddJsonFile(file).Build();
    Assert.Equal(endpoint, config["Storage:Endpoint"]); Assert.Equal(bucket, config["Storage:Bucket"]);
    Assert.Null(config["Storage:AccessKey"]); Assert.Null(config["Storage:SecretKey"]);
  }

  [Theory]
  [InlineData("http://localhost:18333", true)]
  [InlineData("https://storage.getitdone.com.ph", true)]
  [InlineData("http://storage.getitdone.com.ph", false)]
  [InlineData("https://user:secret@storage.example", false)]
  [InlineData("https://storage.example?token=secret", false)]
  public void StorageEndpointValidationRestrictsInsecureAndCredentialBearingUrls(string endpoint, bool valid)
  {
    Assert.Equal(valid, new DocumentStorageOptions { Endpoint = endpoint, Bucket = "test-bucket", AccessKey = "test", SecretKey = "test" }.IsValid());
  }
}
