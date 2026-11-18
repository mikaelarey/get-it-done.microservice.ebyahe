using System.Security.Cryptography;
using System.Text;
using EByahe.Infrastructure.Persistence;
using EByahe.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace EByahe.Tests;

public sealed class GeographySeedBatchTests
{
  [Fact]
  public void GeographyMigrationUsesTransactionalBatchesOfAtMost500Rows()
  {
    var migration = new AddCodeDecodeGeography();
    var batches = migration.UpOperations.OfType<SqlOperation>().ToArray();
    Assert.Equal(91, batches.Length);
    Assert.All(batches, batch => {
      Assert.False(batch.SuppressTransaction);
      Assert.InRange(batch.Sql.Split('\n').Count(line => line.StartsWith("('", StringComparison.Ordinal)), 1, 500);
      Assert.DoesNotContain("\nGO\n", batch.Sql);
      Assert.Contains("IDENTITY_INSERT", batch.Sql);
    });
    Assert.Contains("[CodeDecodeRegions]", batches[0].Sql);
    Assert.Contains("[CodeDecodeProvinces]", batches[1].Sql);
    Assert.Contains("[CodeDecodeMunicipalities]", batches[2].Sql);
    Assert.Contains("[CodeDecodeBarangays]", batches[6].Sql);
  }
}

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task GeographyMigrationPreservesEverySourceCodeNameAndRelationshipAcrossRerunAndRollback()
  {
    await using var db = CreateDatabase();
    async Task VerifySourceData()
    {
      var regions = await db.CodeDecodeRegions.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync();
      var provinces = await db.CodeDecodeProvinces.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync();
      var municipalities = await db.CodeDecodeMunicipalities.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync();
      var barangays = await db.CodeDecodeBarangays.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync();
      Assert.Equal(17, regions.Length); Assert.Equal(82, provinces.Length);
      Assert.Equal(1634, municipalities.Length); Assert.Equal(42046, barangays.Length);
      // Fingerprints calculated from every parsed row in the four supplied SQL files.
      void Verify(string prefix, string expectedHash, IEnumerable<(Guid Id, int Code, string Parent, string Decode, string Creator, DateTimeOffset Created)> rows)
      {
        var content = new StringBuilder();
        foreach (var row in rows)
        {
          Assert.Equal(Guid.Parse($"{prefix}-0000-4000-8000-{row.Code:000000000000}"), row.Id);
          Assert.Equal("System", row.Creator); Assert.NotEqual(default, row.Created);
          content.Append(row.Code).Append('\t').Append(row.Parent).Append('\t').Append(row.Decode).Append('\n');
        }
        Assert.Equal(expectedHash, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString()))));
      }
      Verify("a0010001", "708a4e1fd9662a442c37e5292e5f166235ebbede542c1cdce5555f710d7b3c8e", regions.Select(x => (x.Id, x.Code, x.Description, x.Decode, x.CreatedBy, x.DateTimeCreated)));
      Verify("a0010002", "adfb70a26d924c8da556cd07d4feeda99e4d1a7836b04c49159d5d72557ea927", provinces.Select(x => (x.Id, x.Code, x.CodeDecodeRegion.ToString(), x.Decode, x.CreatedBy, x.DateTimeCreated)));
      Verify("a0010003", "3599081a66ed55b75b63d89bdf08cedb1c7c516593fe6abe4ea7819241bf6396", municipalities.Select(x => (x.Id, x.Code, x.CodeDecodeProvince.ToString(), x.Decode, x.CreatedBy, x.DateTimeCreated)));
      Verify("a0010004", "f1c9918f849f1a7420c12659c6e6c7993f32e1d04749785325034077284dba83", barangays.Select(x => (x.Id, x.Code, x.CodeDecodeMunicipality.ToString(), x.Decode, x.CreatedBy, x.DateTimeCreated)));
      var lasPinas = await db.CodeDecodeMunicipalities.AsNoTracking().Include(x => x.Province).ThenInclude(x => x.Region).SingleAsync(x => x.Code == 1360);
      Assert.Equal("Las Piñas City", lasPinas.Decode); Assert.Equal("Metro Manila", lasPinas.Province.Decode); Assert.Equal("NCR", lasPinas.Province.Region.Decode);
      var apostrophe = await db.CodeDecodeBarangays.AsNoTracking().SingleAsync(x => x.Code == 7876);
      Assert.Equal("O'Donnell", apostrophe.Decode);
      Assert.False(db.Database.HasPendingModelChanges());
      Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1").SingleAsync());
    }
    await VerifySourceData();
    await db.Database.MigrateAsync(); // Applying again must not insert duplicates.
    await VerifySourceData();
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261118030605_AddBusPartnerFleetPlateUniqueness");
    Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name IN ('CodeDecodeRegions', 'CodeDecodeProvinces', 'CodeDecodeMunicipalities', 'CodeDecodeBarangays')").SingleAsync());
    await migrator.MigrateAsync();
    await VerifySourceData();
  }
}
