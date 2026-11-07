using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using EByahe.Domain.Interfaces;
using EByahe.Domain.DataModels;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private sealed class PartnerApiFactory(string connectionString) : WebApplicationFactory<Program>
  {
    public const string Subject = "keycloak-applicant-123";
    private const string Issuer = "https://keycloak.example/realms/ebyahe-tests";
    private const string Audience = "get-it-done-ebyahe-api";
    private readonly RSA signingKey = RSA.Create(2048);
    public MemoryDocumentStorage Storage { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Testing");
      builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
        new Dictionary<string, string?>
        {
          ["ConnectionStrings:EByaheDatabase"] = connectionString,
          ["Kafka:Enabled"] = "false",
          ["Keycloak:Authority"] = Issuer,
          ["Keycloak:Audience"] = Audience,
          ["Cors:AllowedOrigins:0"] = "http://localhost:4204"
        }));
      builder.ConfigureTestServices(services =>
      {
        services.RemoveAll<IDocumentStorage>();
        services.AddSingleton<IDocumentStorage>(Storage);
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
        var discovery = new OpenIdConnectConfiguration { Issuer = Issuer };
        discovery.SigningKeys.Add(new RsaSecurityKey(signingKey.ExportParameters(false)) { KeyId = "tests" });
          options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(discovery);
        });
      });
    }

    public HttpClient AuthenticatedClient()
    {
      var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
      client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
      return client;
    }

    public string Token(string? subject = Subject, string audience = Audience, string issuer = Issuer,
      bool expired = false, bool trusted = true, string type = "Bearer", object? resourceAccess = null, object? realmAccess = null)
    {
      using var other = RSA.Create(2048);
      var claims = new List<Claim> { new("typ", type) };
      if (subject is not null) claims.Add(new Claim("sub", subject));
      if (resourceAccess is not null) claims.Add(new Claim("resource_access", JsonSerializer.Serialize(resourceAccess), JsonClaimValueTypes.Json));
      if (realmAccess is not null) claims.Add(new Claim("realm_access", JsonSerializer.Serialize(realmAccess), JsonClaimValueTypes.Json));
      var key = new RsaSecurityKey(trusted ? signingKey : other) { KeyId = "tests" };
      var token = new JwtSecurityToken(issuer, audience, claims,
        DateTime.UtcNow.AddHours(-1), expired ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
        new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
      return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      if (disposing) signingKey.Dispose();
    }
  }

  private static readonly byte[] Pdf = "%PDF-1.7\nTest application document\n%%EOF"u8.ToArray();

  [SqlServerFact]
  public async Task PartnerEndpointRejectsAnonymousInvalidExpiredAndForgedTokensBeforeSaving()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var tokens = new string?[] { null, "invalid", factory.Token(expired: true), factory.Token(trusted: false),
      factory.Token(audience: "get-it-done-account-api"), factory.Token(issuer: "https://foreign.example/realms/test"),
      factory.Token(subject: null), factory.Token(subject: " "), factory.Token(subject: " padded "),
      factory.Token(subject: new string('x', 256)), factory.Token(type: "ID") };
    foreach (var token in tokens)
    {
      client.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
      using var form = ApplicationForm();
      using var response = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    await using var database = CreateDatabase();
    Assert.Empty(await database.PartnerApplications.ToListAsync());
    Assert.Empty(await database.PartnerApplicationHistories.ToListAsync());
    Assert.Empty(await database.PartnerApplicationApprovals.ToListAsync());
    Assert.Empty(await database.OutboxMessages.ToListAsync());
    Assert.Empty(await database.Outgoing.ToListAsync());
  }

  [SqlServerFact]
  public async Task CreatedByComesFromValidatedTokenAndCannotBeSpoofedInMultipartFields()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    form.Add(new StringContent("another-user"), "createdBy");
    using var response = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    await using var database = CreateDatabase();
    Assert.Equal(PartnerApiFactory.Subject, (await database.PartnerApplications.SingleAsync()).CreatedBy);
  }

  [SqlServerFact]
  public async Task FieldAndDocumentChangesCaptureCompleteBeforeAndAfterSnapshots()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var response = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var receipt = (await response.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    await using var database = CreateDatabase();
    // Deliberately do not load Documents; audit snapshots must still retain all five files.
    var application = await database.PartnerApplications.SingleAsync();
    application.BusinessName = "Updated Partner Company";
    application.Status = "UnderReview";
    await database.SaveChangesAsync();
    var edit = await database.PartnerApplicationHistories.OrderByDescending(row => row.DateTimeCreated).FirstAsync();
    using var before = JsonDocument.Parse(edit.PreviousValue!);
    using var after = JsonDocument.Parse(edit.CurrentValue);
    Assert.Equal("Example Partner Company", before.RootElement.GetProperty("BusinessName").GetString());
    Assert.Equal("Updated Partner Company", after.RootElement.GetProperty("BusinessName").GetString());
    Assert.Equal("UnderReview", edit.Status);
    Assert.Equal(5, after.RootElement.GetProperty("Documents").GetArrayLength());

    var file = await database.PartnerApplicationDocuments.SingleAsync(row => row.DocumentType == "permit");
    var updated = "%PDF-1.7\nUpdated permit\n%%EOF"u8.ToArray();
    var oldStorageKey = file.StorageKey;
    file.SizeBytes = updated.Length;
    file.Sha256 = Convert.ToHexString(SHA256.HashData(updated));
    file.StorageKey = DocumentStorageKey.Create(file.PartnerApplicationId, file.Id, file.Sha256);
    await factory.Storage.PutAsync(file.StorageKey, updated, file.ContentType, file.Sha256);
    database.SaveChanges();
    var documentEdit = await database.PartnerApplicationHistories.OrderByDescending(row => row.DateTimeCreated).FirstAsync();
    using var previousDocument = JsonDocument.Parse(documentEdit.PreviousValue!);
    using var currentDocument = JsonDocument.Parse(documentEdit.CurrentValue);
    var previousReference = previousDocument.RootElement.GetProperty("Documents").EnumerateArray()
      .Single(row => row.GetProperty("DocumentType").GetString() == "permit");
    var currentReference = currentDocument.RootElement.GetProperty("Documents").EnumerateArray()
      .Single(row => row.GetProperty("DocumentType").GetString() == "permit");
    Assert.False(previousReference.TryGetProperty("Content", out _));
    Assert.Equal(oldStorageKey, previousReference.GetProperty("StorageKey").GetString());
    Assert.Equal(file.StorageKey, currentReference.GetProperty("StorageKey").GetString());
    Assert.Equal(Pdf, factory.Storage.Objects[oldStorageKey]);
    Assert.Equal(updated, factory.Storage.Objects[file.StorageKey]);
    Assert.Equal(3, await database.PartnerApplicationHistories.CountAsync());
    await database.SaveChangesAsync();
    Assert.Equal(3, await database.PartnerApplicationHistories.CountAsync());
    database.PartnerApplicationDocuments.Remove(file);
    await database.SaveChangesAsync();
    var removal = await database.PartnerApplicationHistories.OrderByDescending(row => row.DateTimeCreated).FirstAsync();
    using var removed = JsonDocument.Parse(removal.CurrentValue);
    Assert.Equal(4, removed.RootElement.GetProperty("Documents").GetArrayLength());
    Assert.Equal(receipt.Id, removal.PartnerApplicationId);
  }

  [SqlServerFact]
  public async Task PartnerHistoryAndApprovalEntriesAreAppendOnlyAndCreatorIsImmutable()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var response = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    await using var database = CreateDatabase();
    var history = await database.PartnerApplicationHistories.SingleAsync();
    history.CurrentValue = "{}";
    await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
    database.ChangeTracker.Clear();
    var approval = await database.PartnerApplicationApprovals.SingleAsync();
    database.PartnerApplicationApprovals.Remove(approval);
    Assert.Throws<InvalidOperationException>(() => database.SaveChanges());
    database.ChangeTracker.Clear();
    var application = await database.PartnerApplications.SingleAsync();
    application.CreatedBy = "another-user";
    await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
    database.ChangeTracker.Clear();
    database.PartnerApplicationApprovals.Add(new PartnerApplicationApproval
    {
      Id = Guid.NewGuid(), PartnerApplicationId = application.Id, PreviousStatus = "Pending", Status = "UnderReview",
      ChangedBy = "reviewer-subject", Remarks = "Review started", DateTimeCreated = DateTimeOffset.UtcNow
    });
    await database.SaveChangesAsync();
    Assert.Equal(2, await database.PartnerApplicationApprovals.CountAsync());
    Assert.Equal(PartnerApiFactory.Subject, (await database.PartnerApplications.SingleAsync()).CreatedBy);
  }

  [SqlServerFact]
  public async Task HistoryInsertFailureRollsBackApplicationDocumentsAndInitialApproval()
  {
    await using var database = CreateDatabase();
    await database.Database.ExecuteSqlRawAsync("""
      CREATE TRIGGER [FailPartnerAudit] ON [PartnerApplicationHistories] AFTER INSERT AS
      BEGIN THROW 51000, 'Test audit failure', 1; END
      """);
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var response = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Empty(factory.Storage.Objects);
    Assert.Empty(await database.PartnerApplications.ToListAsync());
    Assert.Empty(await database.PartnerApplicationDocuments.ToListAsync());
    Assert.Empty(await database.PartnerApplicationHistories.ToListAsync());
    Assert.Empty(await database.PartnerApplicationApprovals.ToListAsync());
  }

  [SqlServerFact]
  public async Task StorageFailureReturns503WithoutSavingAnApplicationOrLeavingPartialUploads()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    factory.Storage.FailAtPut = 2;
    using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm();
    using var response = await client.PostAsync("/api/partner-applications", form);
    Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    Assert.Empty(factory.Storage.Objects);
    await using var database = CreateDatabase();
    Assert.Empty(await database.PartnerApplications.ToListAsync());
    Assert.Empty(await database.PartnerApplicationHistories.ToListAsync());
    Assert.Empty(await database.PartnerApplicationApprovals.ToListAsync());
  }

  private static MultipartFormDataContent ApplicationForm(string category = "hotel", string entity = "sole-proprietorship",
    string? omit = null, string? invalidFile = null, string? fileName = null, byte[]? content = null,
    string contentType = "application/pdf")
  {
    var form = new MultipartFormDataContent();
    var fields = new Dictionary<string, string>
    {
      ["businessCategory"] = category,
      ["corporate.businessName"] = "Example Partner Company",
      ["corporate.tradeName"] = "Example Partner",
      ["corporate.entityType"] = entity,
      ["corporate.tin"] = "123-456-789",
      ["corporate.address"] = "123 Test Street, Manila",
      ["corporate.representativeName"] = "Maria Santos",
      ["corporate.representativeEmail"] = "maria@example.com",
      ["corporate.representativeMobile"] = "+63 917 123 4567",
      ["payout.bankName"] = "BPI",
      ["payout.accountNumber"] = "1234-5678-90",
      ["payout.accountHolder"] = "Example Partner Company",
      ["payout.settlementFrequency"] = "weekly"
    };
    foreach (var (key, value) in fields)
      if (key != omit) form.Add(new StringContent(value), key);
    foreach (var kind in new[] { "registration", "bir", "permit", "accreditation", "bankAccountProof" })
    {
      if ($"documents.{kind}" == omit) continue;
      var file = new ByteArrayContent(kind == invalidFile ? content ?? [] : Pdf);
      file.Headers.ContentType = new MediaTypeHeaderValue(kind == invalidFile ? contentType : "application/pdf");
      form.Add(file, $"documents.{kind}", kind == invalidFile ? fileName ?? "invalid.pdf" : $"{kind}.pdf");
    }
    return form;
  }

  [SqlServerFact]
  public async Task MultipartEndpointSavesAllApplicationFieldsAndFiveDocuments()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    foreach (var (category, entity) in new[] { ("hotel", "sole-proprietorship"), ("bus-operator", "corporation") })
    {
      var subject = $"{PartnerApiFactory.Subject}-{category}";
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: subject));
      using var form = ApplicationForm(category, entity);
      using var response = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.Created, response.StatusCode);
      var receipt = (await response.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
      Assert.Equal("Submitted", receipt.Status);
      Assert.Matches("^APP-[A-F0-9]{32}$", receipt.TrackingId);
      await using var database = CreateDatabase();
      var application = await database.PartnerApplications.Include(row => row.Documents)
        .SingleAsync(row => row.Id == receipt.Id);
      Assert.Equal(category, application.BusinessCategory);
      Assert.Equal(entity, application.EntityType);
      Assert.Equal("Example Partner Company", application.BusinessName);
      Assert.Equal("Example Partner", application.TradeName);
      Assert.Equal("123-456-789", application.Tin);
      Assert.Equal("123 Test Street, Manila", application.Address);
      Assert.Equal("Maria Santos", application.RepresentativeName);
      Assert.Equal("maria@example.com", application.RepresentativeEmail);
      Assert.Equal("+63 917 123 4567", application.RepresentativeMobile);
      Assert.Equal("BPI", application.BankName);
      Assert.Equal("1234-5678-90", application.AccountNumber);
      Assert.Equal("Example Partner Company", application.AccountHolder);
      Assert.Equal("weekly", application.SettlementFrequency);
      Assert.Equal(receipt.SubmittedAtUtc, application.SubmittedAtUtc);
      Assert.Equal(receipt.TrackingId, application.TrackingId);
      Assert.Equal(subject, application.CreatedBy);
      var history = await database.PartnerApplicationHistories.SingleAsync(row => row.PartnerApplicationId == application.Id);
      Assert.Null(history.PreviousValue);
      Assert.Equal("Submitted", history.Status);
      using var snapshot = JsonDocument.Parse(history.CurrentValue);
      Assert.Equal(subject, snapshot.RootElement.GetProperty("CreatedBy").GetString());
      Assert.Equal(5, snapshot.RootElement.GetProperty("Documents").GetArrayLength());
      Assert.All(snapshot.RootElement.GetProperty("Documents").EnumerateArray(), document =>
      {
        Assert.False(document.TryGetProperty("Content", out _));
        Assert.Equal(factory.Storage.Bucket, document.GetProperty("StorageBucket").GetString());
        Assert.Equal(Pdf, factory.Storage.Objects[document.GetProperty("StorageKey").GetString()!]);
      });
      var approval = await database.PartnerApplicationApprovals.SingleAsync(row => row.PartnerApplicationId == application.Id);
      Assert.Equal("Pending", approval.Status);
      Assert.Null(approval.PreviousStatus);
      Assert.Equal(subject, approval.ChangedBy);
      Assert.Equal(receipt.SubmittedAtUtc, approval.DateTimeCreated);
      Assert.Equal(5, application.Documents.Count);
      Assert.Equal(new[] { "accreditation", "bankAccountProof", "bir", "permit", "registration" },
        application.Documents.Select(row => row.DocumentType).Order().ToArray());
      Assert.All(application.Documents, document =>
      {
        Assert.Equal(Pdf, factory.Storage.Objects[document.StorageKey]);
        Assert.Equal(factory.Storage.Bucket, document.StorageBucket);
        Assert.Equal((long)Pdf.Length, document.SizeBytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Pdf)), document.Sha256);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(application.SubmittedAtUtc, document.UploadedAtUtc);
      });
    }
  }

  [SqlServerFact]
  public async Task MultipartEndpointRejectsMissingDetailsAndDocumentsWithoutSavingAnything()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    foreach (var field in new[] { "corporate.businessName", "payout.accountNumber", "documents.registration", "documents.bir", "documents.permit", "documents.accreditation", "documents.bankAccountProof" })
    {
      using var form = ApplicationForm(omit: field);
      using var response = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Contains("errors", await response.Content.ReadAsStringAsync());
    }
    using var badCategory = ApplicationForm(category: "invalid");
    using var invalid = await client.PostAsync("/api/partner-applications", badCategory);
    Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    await using var database = CreateDatabase();
    Assert.Empty(await database.PartnerApplications.ToListAsync());
    Assert.Empty(await database.PartnerApplicationDocuments.ToListAsync());
  }

  [SqlServerFact]
  public async Task MultipartEndpointRejectsSpoofedEmptyAndOversizedFiles()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var cases = new (string Name, byte[] Content, string Type)[]
    {
      ("script.exe", Pdf, "application/pdf"),
      ("fake.pdf", "not-a-pdf"u8.ToArray(), "application/pdf"),
      ("empty.pdf", [], "application/pdf"),
      ("wrong-mime.pdf", Pdf, "image/png"),
      ("large.pdf", new byte[PartnerApplicationDocument.MaxFileSize + 1], "application/pdf")
    };
    foreach (var item in cases)
    {
      using var form = ApplicationForm(invalidFile: "accreditation", fileName: item.Name, content: item.Content, contentType: item.Type);
      using var response = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Contains("documents.accreditation", await response.Content.ReadAsStringAsync());
    }
    await using var database = CreateDatabase();
    Assert.Empty(await database.PartnerApplications.ToListAsync());
    Assert.Empty(await database.PartnerApplicationDocuments.ToListAsync());
  }

  [SqlServerFact]
  public async Task MultipartEndpointAcceptsSupportedFormatsAndSanitizesOriginalFileNames()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    foreach (var item in PartnerUploadTestFiles.Allowed())
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: Guid.NewGuid().ToString()));
      var name = "../../" + (string)item[0]; var type = (string)item[1]; var bytes = (byte[])item[2];
      using var form = ApplicationForm(invalidFile: "bankAccountProof", fileName: name, content: bytes, contentType: type);
      using var response = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.Created, response.StatusCode);
      var receipt = (await response.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
      await using var database = CreateDatabase();
      var document = await database.PartnerApplicationDocuments.SingleAsync(row => row.PartnerApplicationId == receipt.Id && row.DocumentType == "bankAccountProof");
      Assert.Equal(Path.GetFileName(name), document.FileName);
      Assert.Equal(type, document.ContentType);
      Assert.Equal(bytes, factory.Storage.Objects[document.StorageKey]);
    }
  }

  [SqlServerFact]
  public async Task ApplicationAndDocumentInsertsRollBackTogetherOnDatabaseFailure()
  {
    await using var database = CreateDatabase();
    var application = new PartnerApplication
    {
      Id = Guid.NewGuid(), TrackingId = "APP-ROLLBACK", BusinessCategory = "hotel", CreatedBy = PartnerApiFactory.Subject,
      BusinessName = "Rollback", TradeName = "Rollback", EntityType = "corporation", Tin = "123",
      Address = "Manila", RepresentativeName = "Maria", RepresentativeEmail = "maria@example.com",
      RepresentativeMobile = "+639171234567", BankName = "BPI", AccountNumber = "123", AccountHolder = "Maria",
      SettlementFrequency = "daily", SubmittedAtUtc = DateTimeOffset.UtcNow
    };
    // Duplicate document kinds violate the database's unique index after the parent insert.
    foreach (var _ in Enumerable.Range(0, 2))
      application.Documents.Add(new PartnerApplicationDocument
      {
        Id = Guid.NewGuid(), PartnerApplicationId = application.Id, DocumentType = "registration",
        FileName = "test.pdf", ContentType = "application/pdf", SizeBytes = Pdf.Length,
        StorageBucket = "ebyahe-test-documents", StorageKey = "ebyahe/partner-applications/rollback/test",
        Sha256 = Convert.ToHexString(SHA256.HashData(Pdf)), UploadedAtUtc = DateTimeOffset.UtcNow
      });
    await Assert.ThrowsAsync<DbUpdateException>(() => new PartnerApplicationRepository(database).AddAsync(application));
    database.ChangeTracker.Clear();
    Assert.Empty(await database.PartnerApplications.ToListAsync());
    Assert.Empty(await database.PartnerApplicationDocuments.ToListAsync());
    Assert.Empty(await database.PartnerApplicationHistories.ToListAsync());
    Assert.Empty(await database.PartnerApplicationApprovals.ToListAsync());
  }

  [SqlServerFact]
  public async Task PartnerEndpointSupportsConfiguredCorsAndRejectsJson()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/partner-applications");
    preflight.Headers.Add("Origin", "http://localhost:4204");
    preflight.Headers.Add("Access-Control-Request-Method", "POST");
    using var response = await client.SendAsync(preflight);
    Assert.Equal("http://localhost:4204", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    using var invalid = await client.PostAsJsonAsync("/api/partner-applications", new { businessCategory = "hotel" });
    Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalid.StatusCode);
  }
}
