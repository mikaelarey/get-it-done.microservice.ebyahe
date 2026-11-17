using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using EByahe.Infrastructure.Persistence;
namespace EByahe.Tests;
public sealed class CodeDecodeSchemaTests
{
  [Fact]
  public void ModelUsesCodesForSetupReferencesAndRefUsersForAuditActors()
  {
    using var db = new EByaheDbContext(new DbContextOptionsBuilder<EByaheDbContext>().UseSqlServer("Server=.;Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True").Options);
    var subject = db.Model.GetEntityTypes().Single(x => x.GetTableName() == "RefUsers").FindProperty("ExternalUserId")!;
    Assert.False(subject.GetKeyValueComparer().Equals("OwnerCase", "ownercase"));
    foreach (var entity in db.Model.GetEntityTypes())
    {
      if (entity.GetTableName()!.StartsWith("CodeDecode")) {
        Assert.Equal(typeof(Guid), entity.FindPrimaryKey()!.Properties.Single().ClrType);
        var code = entity.FindProperty("Code")!;
        Assert.Equal(typeof(int), code.ClrType);
        Assert.Contains(entity.GetKeys(), key => key.Properties.SequenceEqual(new[] { code }));
      }
      foreach (var property in entity.GetProperties().Where(x => x.Name.StartsWith("CodeDecode")))
        Assert.Contains(entity.GetForeignKeys(), fk => fk.Properties.Contains(property) && fk.PrincipalKey.Properties.Single().Name == "Code");
      foreach (var property in entity.GetProperties().Where(x => x.Name is "CreatedBy" or "UpdatedBy" or "ChangedBy" or "ChangedById"))
        Assert.Contains(entity.GetForeignKeys(), fk => fk.Properties.Contains(property) && fk.PrincipalEntityType.GetTableName() == "RefUsers");
    }
  }

  [SqlServerFact]
  public async Task SqlServerIndexesCodesGeneratesNextCodeAndRejectsUnknownCreators()
  {
    var databaseName = "Getitdone.EByahe.Tests.CodeDecode." + Guid.NewGuid().ToString("N");
    var connection = new SqlConnectionStringBuilder { DataSource = Environment.GetEnvironmentVariable("EBYAHE_TEST_SQL_SERVER"), InitialCatalog = databaseName, IntegratedSecurity = true, TrustServerCertificate = true };
    await using var db = new EByaheDbContext(new DbContextOptionsBuilder<EByaheDbContext>().UseSqlServer(connection.ConnectionString).Options);
    try {
      await db.Database.MigrateAsync();
      Assert.False(db.Database.HasPendingModelChanges());
      var tables = await db.Database.SqlQueryRaw<string>("SELECT [name] AS [Value] FROM sys.tables WHERE [name] LIKE 'CodeDecode%'").ToArrayAsync();
      Assert.NotEmpty(tables);
      // Identifiers come from this disposable database and are restricted before interpolation.
#pragma warning disable EF1002
      foreach (var table in tables) {
        Assert.Matches("^CodeDecode[A-Za-z]+$", table);
        var identity = await db.Database.SqlQueryRaw<int>($"SELECT CONVERT(int, COLUMNPROPERTY(OBJECT_ID('[{table}]'), 'Code', 'IsIdentity')) AS [Value]").SingleAsync();
        Assert.Equal(1, identity);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('[{table}]') AND i.is_unique=1 AND c.name='Code'").SingleAsync());
        var previous = await db.Database.SqlQueryRaw<int>($"SELECT MAX([Code]) AS [Value] FROM [{table}]").SingleAsync();
        await db.Database.ExecuteSqlRawAsync($"INSERT INTO [{table}] ([Id], [Decode]) VALUES (NEWID(), 'Schema test')");
        Assert.True(await db.Database.SqlQueryRaw<int>($"SELECT [Code] AS [Value] FROM [{table}] WHERE [Decode]='Schema test'").SingleAsync() > previous);
        var invalid = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync($"UPDATE [{table}] SET [CreatedBy]=N'not-a-registered-user' WHERE [Decode]='Schema test'"));
        Assert.Equal(547, invalid.Number);
      }
    }
#pragma warning restore EF1002
    finally { await db.Database.EnsureDeletedAsync(); }
  }
}
