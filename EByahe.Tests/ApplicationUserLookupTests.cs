using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EByahe.Tests;
public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ApplicationUserLookupsPreserveLegacyValuesAndAllowOnlyConfiguredBankUpdates()
  {
    await using var db = CreateDatabase();
    var application = ReviewApplication("Lookup migration", "bus-operator", "Submitted", 1, "lookup-owner");
    application.EntityType = "partnership"; application.BankName = "Land Bank"; application.SettlementFrequency = "weekly";
    var owner = await db.RefUsers.SingleAsync(x => x.ExternalUserId == "lookup-owner"); owner.Role = "Administrator";
    db.PartnerApplications.Add(application); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var migrations = db.Database.GetMigrations().ToArray();
    var index = Array.FindIndex(migrations, id => id.EndsWith("_AddApplicationAndUserSetupLookups", StringComparison.Ordinal)); Assert.True(index > 0);
    var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(migrations[index - 1]);
    Assert.Equal("partnership", await db.Database.SqlQueryRaw<string>("SELECT [EntityType] AS [Value] FROM [PartnerApplications]").SingleAsync());
    Assert.Equal("Land Bank", await db.Database.SqlQueryRaw<string>("SELECT [BankName] AS [Value] FROM [PartnerApplications]").SingleAsync());
    Assert.Equal("weekly", await db.Database.SqlQueryRaw<string>("SELECT [SettlementFrequency] AS [Value] FROM [PartnerApplications]").SingleAsync());
    Assert.Equal("Administrator", await db.Database.SqlQuery<string>($"SELECT [Role] AS [Value] FROM [RefUsers] WHERE [ExternalUserId]={owner.ExternalUserId}").SingleAsync());
    await db.Database.ExecuteSqlRawAsync("UPDATE [PartnerApplications] SET [BankName]=N'Legacy bank'");
    await migrator.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    var saved = await db.PartnerApplications.SingleAsync(x => x.EntityType == "partnership" && x.BankName == "Legacy bank" && x.SettlementFrequency == "weekly");
    Assert.Equal("Legacy bank", saved.BankName); Assert.NotNull(saved.CodeDecodeBank);
    Assert.Equal("Administrator", (await db.RefUsers.SingleAsync(x => x.Role == "Administrator")).Role);
    Assert.Equal(1, (await db.RefUsers.SingleAsync(x => x.ExternalUserId == "System")).CodeDecodeRole);
    var originalLogs = await db.PartnerApplicationHistories.CountAsync();
    saved.BankName = "Maya Bank"; db.SaveChanges();
    Assert.Equal("Maya Bank", saved.BankName); Assert.Equal(originalLogs + 1, await db.PartnerApplicationHistories.CountAsync());
    db.ChangeTracker.Clear(); Assert.Equal("Maya Bank", (await db.PartnerApplications.SingleAsync()).BankName);
    var unknown = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE [PartnerApplications] SET [CodeDecodeBank]=999999")); Assert.Equal(547, unknown.Number);
    var unknownRole = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE [RefUsers] SET [CodeDecodeRole]=999999")); Assert.Equal(547, unknownRole.Number);
  }

  [SqlServerFact]
  public async Task BankOptionsComeFromSetupAndUnlistedNamesFailBeforeUploads()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var options = await client.GetAsync("/api/partner-applications/banks"); Assert.Equal(HttpStatusCode.OK, options.StatusCode);
    var names = (await options.Content.ReadFromJsonAsync<string[]>())!;
    Assert.Contains("BDO Unibank, Inc.", names); Assert.Contains("Maribank", names); Assert.Contains("Overseas Filipino Bank (OFBank)", names);
    Assert.Equal(36, names.Length);
    using var form = ApplicationForm(omit: "payout.bankName");
    form.Add(new StringContent("Unlisted Bank"), "payout.bankName");
    using var result = await client.PostAsync("/api/partner-applications", form); Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    Assert.Contains("payout.bankName", await result.Content.ReadAsStringAsync()); Assert.Empty(factory.Storage.Objects);
    await using var db = CreateDatabase(); Assert.Empty(await db.PartnerApplications.ToArrayAsync()); Assert.Equal(36, await db.CodeDecodeBanks.CountAsync());
    client.DefaultRequestHeaders.Authorization = null;
    using var guest = await client.GetAsync("/api/partner-applications/banks"); Assert.Equal(HttpStatusCode.Unauthorized, guest.StatusCode);
  }
}
