using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using EByahe.Infrastructure;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EByahe.Tests;

public sealed class PartnerStorageMigrationTests
{
  private static readonly byte[] OldFile = "%PDF-1.7\nHistorical version\n%%EOF"u8.ToArray();
  private static readonly byte[] CurrentFile = "%PDF-1.7\nCurrent version\n%%EOF"u8.ToArray();

  [SqlServerFact]
  public async Task MovesCurrentAndHistoricalVersionsAndCanSafelyRetryAfterVerificationFailure()
  {
    var name = "Getitdone.Ebyahe.Tests." + Guid.NewGuid().ToString("N");
    var connection = new SqlConnectionStringBuilder
    {
      DataSource = Environment.GetEnvironmentVariable("EBYAHE_TEST_SQL_SERVER"), InitialCatalog = name,
      IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true
    }.ConnectionString;
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      { ["ConnectionStrings:EByaheDatabase"] = connection }).Build();
    var storage = new MemoryDocumentStorage { CorruptReads = true };
    await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration)
      .AddSingleton<IDocumentStorage>(storage).BuildServiceProvider();
    await using var database = new EByaheDbContext(new DbContextOptionsBuilder<EByaheDbContext>().UseSqlServer(connection).Options);
    try
    {
      var prepare = database.Database.GetMigrations().Single(id => id.EndsWith("_AddPartnerDocumentStorageReferences"));
      await database.GetService<IMigrator>().MigrateAsync(prepare);
      var application = new PartnerApplication
      {
        Id = Guid.NewGuid(), TrackingId = "APP-LEGACY", CreatedBy = "legacy-user", BusinessCategory = "hotel",
        BusinessName = "Legacy Company", TradeName = "Legacy", EntityType = "corporation", Tin = "123", Address = "Manila",
        RepresentativeName = "Test", RepresentativeEmail = "test@example.com", RepresentativeMobile = "+639171234567",
        BankName = "Test", AccountNumber = "123", AccountHolder = "Test", SettlementFrequency = "weekly", SubmittedAtUtc = DateTimeOffset.UtcNow
      };
      var document = new PartnerApplicationDocument
      {
        Id = Guid.NewGuid(), PartnerApplicationId = application.Id, DocumentType = "permit", FileName = "permit.pdf",
        ContentType = "application/pdf", SizeBytes = CurrentFile.Length, Sha256 = Convert.ToHexString(SHA256.HashData(CurrentFile)),
        StorageBucket = storage.Bucket, StorageKey = "ebyahe/partner-applications/temporary-seed", UploadedAtUtc = application.SubmittedAtUtc
      };
      application.Documents.Add(document);
      database.PartnerApplications.Add(application);
      var approval = new PartnerApplicationApproval
      {
        Id = Guid.NewGuid(), PartnerApplicationId = application.Id, Status = "Pending", ChangedBy = "legacy-user", DateTimeCreated = application.SubmittedAtUtc
      };
      database.PartnerApplicationApprovals.Add(approval);
      await database.SaveChangesAsync();
      var firstHistory = await database.PartnerApplicationHistories.SingleAsync();
      var firstId = firstHistory.Id;
      var secondId = Guid.NewGuid();
      var oldJson = LegacySnapshot(application, OldFile);
      var currentJson = LegacySnapshot(application, CurrentFile);
      var secondDate = application.SubmittedAtUtc.AddSeconds(1);
      await database.Database.ExecuteSqlInterpolatedAsync($"""
        UPDATE [PartnerApplicationDocuments] SET [Content] = {CurrentFile}, [StorageBucket] = NULL, [StorageKey] = NULL WHERE [Id] = {document.Id};
        UPDATE [PartnerApplications] SET [CreatedBy] = NULL WHERE [Id] = {application.Id};
        UPDATE [PartnerApplicationHistories] SET [PreviousValue] = NULL, [CurrentValue] = {oldJson} WHERE [Id] = {firstId};
        INSERT INTO [PartnerApplicationHistories] ([Id], [PartnerApplicationId], [PreviousValue], [CurrentValue], [Status], [DateTimeCreated])
        VALUES ({secondId}, {application.Id}, {oldJson}, {currentJson}, 'Submitted', {secondDate});
        """);
      database.ChangeTracker.Clear();
      await Assert.ThrowsAsync<InvalidOperationException>(() => services.InitializeInfrastructureAsync());
      Assert.Equal(CurrentFile.Length, await database.Database.SqlQueryRaw<long>(
        "SELECT COALESCE(SUM(DATALENGTH([Content])),0) AS [Value] FROM [PartnerApplicationDocuments]").SingleAsync());
      var guard = await Assert.ThrowsAsync<SqlException>(() => database.GetService<IMigrator>().MigrateAsync());
      Assert.Contains("copy and verify", guard.Message);

      storage.CorruptReads = false;
      await services.InitializeInfrastructureAsync();
      Assert.Equal(0, await database.Database.SqlQueryRaw<int>(
        "SELECT CASE WHEN COL_LENGTH('PartnerApplicationDocuments', 'Content') IS NULL THEN 0 ELSE 1 END AS [Value]").SingleAsync());
      var migrated = await database.PartnerApplicationDocuments.SingleAsync();
      Assert.Equal(CurrentFile, storage.Objects[migrated.StorageKey]);
      Assert.Equal(storage.Bucket, migrated.StorageBucket);
      Assert.Null((await database.PartnerApplications.SingleAsync()).CreatedBy);
      var histories = await database.PartnerApplicationHistories.OrderBy(row => row.DateTimeCreated).ToListAsync();
      Assert.Equal(new[] { firstId, secondId }, histories.Select(history => history.Id));
      Assert.Null(histories[0].PreviousValue);
      Assert.Equal(application.SubmittedAtUtc, histories[0].DateTimeCreated);
      Assert.Equal(secondDate, histories[1].DateTimeCreated);
      Assert.Equal(OldFile, FileFromSnapshot(histories[0].CurrentValue, storage));
      Assert.Equal(OldFile, FileFromSnapshot(histories[1].PreviousValue!, storage));
      Assert.Equal(CurrentFile, FileFromSnapshot(histories[1].CurrentValue, storage));
      Assert.Equal(2, storage.Objects.Count);
      Assert.Equal(approval.Id, (await database.PartnerApplicationApprovals.SingleAsync()).Id);
      var calls = storage.PutCalls;
      await services.InitializeInfrastructureAsync();
      Assert.Equal(calls, storage.PutCalls);
    }
    finally
    {
      if (!Guid.TryParseExact(name["Getitdone.Ebyahe.Tests.".Length..], "N", out _)) throw new InvalidOperationException("Unsafe test cleanup name.");
      await database.Database.EnsureDeletedAsync();
    }
  }

  private static string LegacySnapshot(PartnerApplication application, byte[] bytes)
  {
    var snapshot = JsonSerializer.SerializeToNode(application)!.AsObject();
    snapshot["CreatedBy"] = null;
    foreach (var item in snapshot["Documents"]!.AsArray())
    {
      var document = item!.AsObject();
      document.Remove("StorageBucket"); document.Remove("StorageKey");
      document["Content"] = Convert.ToBase64String(bytes);
      document["SizeBytes"] = bytes.Length;
      document["Sha256"] = Convert.ToHexString(SHA256.HashData(bytes));
    }
    return snapshot.ToJsonString();
  }

  private static byte[] FileFromSnapshot(string json, MemoryDocumentStorage storage)
  {
    using var snapshot = JsonDocument.Parse(json);
    var document = snapshot.RootElement.GetProperty("Documents")[0];
    Assert.False(document.TryGetProperty("Content", out _));
    Assert.Equal(storage.Bucket, document.GetProperty("StorageBucket").GetString());
    return storage.Objects[document.GetProperty("StorageKey").GetString()!];
  }
}
