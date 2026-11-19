using EByahe.Domain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task DocumentTypeMigrationPreservesAllDocumentsAndMetadataAndEnforcesCodesAndUniqueness()
  {
    await using var db = CreateDatabase();
    var application = ReviewApplication("Document migration", "bus-operator", "Submitted", 1, "document-type-owner");
    var kinds = new[] { "registration", "bir", "permit", "accreditation", "bankAccountProof" };
    application.Documents = kinds.Select(kind => new PartnerApplicationDocument {
      Id = Guid.NewGuid(), PartnerApplicationId = application.Id, DocumentType = kind, FileName = kind + ".pdf",
      ContentType = "application/pdf", SizeBytes = 1, Sha256 = new string('A', 64), StorageBucket = "test-documents",
      StorageKey = "private/" + kind + ".pdf", UploadedAtUtc = DateTimeOffset.UtcNow
    }).ToList();
    var expected = application.Documents.OrderBy(x => x.DocumentType).Select(x => new {
      x.Id, x.DocumentType, x.FileName, x.ContentType, x.SizeBytes, x.Sha256, x.StorageBucket, x.StorageKey, x.UploadedAtUtc
    }).ToArray();
    db.PartnerApplications.Add(application); await db.SaveChangesAsync();
    var historyCount = await db.PartnerApplicationHistories.CountAsync(); db.ChangeTracker.Clear();
    var migrations = db.Database.GetMigrations().ToArray();
    var index = Array.FindIndex(migrations, id => id.EndsWith("_AddDocumentTypeCodeDecode", StringComparison.Ordinal));
    Assert.True(index > 0);
    var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(migrations[index - 1]);
    Assert.Equal(kinds.Order().ToArray(), await db.Database.SqlQueryRaw<string>("SELECT [DocumentType] AS [Value] FROM [PartnerApplicationDocuments] ORDER BY [DocumentType]").ToArrayAsync());
    await migrator.MigrateAsync();
    Assert.False(db.Database.HasPendingModelChanges());
    Assert.Equal(new[] { "Registration", "BIR", "Permit", "Accreditation", "Bank Account Proof" }, await db.CodeDecodeDocumentTypes.OrderBy(x => x.Code).Select(x => x.Decode).ToArrayAsync());
    Assert.Equal(kinds, await db.PartnerApplicationDocuments.OrderBy(x => x.CodeDecodeDocumentType).Select(x => x.DocumentType).ToArrayAsync());
    Assert.Equal(5, (await db.PartnerApplicationDocuments.SingleAsync(x => x.DocumentType == "bankAccountProof")).CodeDecodeDocumentType);
    var actual = (await db.PartnerApplicationDocuments.AsNoTracking().ToArrayAsync()).OrderBy(x => x.DocumentType).Select(x => new {
      x.Id, x.DocumentType, x.FileName, x.ContentType, x.SizeBytes, x.Sha256, x.StorageBucket, x.StorageKey, x.UploadedAtUtc
    }).ToArray();
    Assert.Equal(expected, actual); Assert.Equal(historyCount, await db.PartnerApplicationHistories.CountAsync());
    var unknown = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [PartnerApplicationDocuments] SET [CodeDecodeDocumentType]=999 WHERE [PartnerApplicationId]={application.Id}"));
    Assert.Equal(547, unknown.Number);
    var duplicate = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO [PartnerApplicationDocuments] ([Id],[PartnerApplicationId],[CodeDecodeDocumentType],[FileName],[ContentType],[SizeBytes],[Sha256],[StorageBucket],[StorageKey],[UploadedAtUtc])
      SELECT NEWID(),[PartnerApplicationId],[CodeDecodeDocumentType],[FileName],[ContentType],[SizeBytes],[Sha256],[StorageBucket],[StorageKey],[UploadedAtUtc]
      FROM [PartnerApplicationDocuments] WHERE [PartnerApplicationId]={application.Id} AND [CodeDecodeDocumentType]=1
      """));
    Assert.Contains(duplicate.Number, new[] { 2601, 2627 });
    var used = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM [CodeDecodeDocumentTypes] WHERE [Code]=1"));
    Assert.Equal(547, used.Number);
  }
}
