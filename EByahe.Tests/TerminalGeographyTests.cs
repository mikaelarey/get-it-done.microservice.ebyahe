using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task TerminalAddressesIncludeGeographyForManualAndImportedRowsAndPreserveLegacyAddresses()
  {
    var partner = await SeedMemberPartner();
    await using var db = CreateDatabase();
    var legacy = new BusTerminal { PartnerId = partner.Id, Name = "Legacy terminal", Address = "Legacy street",
      Location = new Location { Code = "LEGACY-ADDRESS", Name = "Legacy city" } };
    db.BusTerminals.Add(legacy); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    const string api = "/api/partner/terminals";
    const string expected = "100 Main street, Adams (Pob.), Adams, Ilocos Norte, Region I";
    var command = new { partnerId = partner.Id, codeDecodeRegion = 3, codeDecodeProvince = 2, codeDecodeMunicipality = 1,
      codeDecodeBarangay = 1, name = "Manual terminal", address = "100 Main street" };
    using var created = await client.PostAsJsonAsync(api, command); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    Assert.Equal(expected, (await created.Content.ReadFromJsonAsync<PartnerPortalTerminal>())!.Address);
    using var form = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid("Imported terminal"));
    Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(api + "/import", form)).StatusCode);
    var portal = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTerminal>>(api))!;
    Assert.Equal(3, portal.TotalCount);
    Assert.Equal("Legacy street", portal.Items.Single(row => row.Id == legacy.Id).Address);
    Assert.All(portal.Items.Where(row => row.Id != legacy.Id), row => {
      Assert.Equal(expected, row.Address); Assert.Equal("Adams (Ilocos Norte)", row.Location);
    });
    var directory = await new EByahe.Infrastructure.Repositories.PartnerDirectoryRepository(db).TerminalsAsync(partner.Id, 1, 10, default);
    Assert.All(directory!.Items.Where(row => row.Id != legacy.Id), row => Assert.Equal(expected, row.Address));
    Assert.Equal("Legacy street", directory.Items.Single(row => row.Id == legacy.Id).Address);
    Assert.All(await db.BusTerminals.AsNoTracking().Where(row => row.Id != legacy.Id).ToArrayAsync(), row => Assert.Equal("100 Main street", row.Address));
    var nextPage = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTerminal>>(api + "?page=2&pageSize=1"))!;
    Assert.Equal(3, nextPage.TotalCount); Assert.Single(nextPage.Items);
  }

  [SqlServerFact]
  public async Task TerminalGeographyOptionsFilterByParentAndCreationReusesLocation()
  {
    var partner = await SeedMemberPartner();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    const string api = "/api/partner/terminals";
    var regions = (await client.GetFromJsonAsync<GeographyOption[]>(api + "/regions"))!;
    Assert.Equal(17, regions.Length);
    Assert.Equal(new GeographyOption(1, "NCR"), regions[0]);
    Assert.Equal(new[] { new GeographyOption(1, "Metro Manila") }, await client.GetFromJsonAsync<GeographyOption[]>(api + "/provinces?codeDecodeRegion=1"));
    var cities = (await client.GetFromJsonAsync<GeographyOption[]>(api + "/municipalities?codeDecodeProvince=1"))!;
    Assert.Contains(cities, row => row.Code == 1354 && row.Decode == "Quezon City"); Assert.DoesNotContain(cities, row => row.Code == 1);
    var barangays = (await client.GetFromJsonAsync<GeographyOption[]>(api + "/barangays?codeDecodeMunicipality=1354"))!;
    await using var db = CreateDatabase();
    Assert.Equal(await db.CodeDecodeBarangays.CountAsync(row => row.CodeDecodeMunicipality == 1354), barangays.Length);
    Assert.DoesNotContain(barangays, row => row.Code == 1);
    foreach (var invalid in new[] { "/provinces", "/municipalities?codeDecodeProvince=0", "/barangays?codeDecodeMunicipality=-1" })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(api + invalid)).StatusCode);
    Assert.Empty((await client.GetFromJsonAsync<GeographyOption[]>(api + "/barangays?codeDecodeMunicipality=2147483647"))!);
    var command = new { partnerId = partner.Id, codeDecodeRegion = 1, codeDecodeProvince = 1, codeDecodeMunicipality = 1354,
      codeDecodeBarangay = barangays[0].Code, name = "Cubao terminal", address = "100 Main street" };
    var locationsBefore = await db.Locations.CountAsync();
    var responses = await Task.WhenAll(client.PostAsJsonAsync(api, command), client.PostAsJsonAsync(api, command with { name = "Other terminal" }));
    foreach (var response in responses)
    {
      using (response)
      {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Quezon City (Metro Manila)", (await response.Content.ReadFromJsonAsync<PartnerPortalTerminal>())!.Location);
      }
    }
    Assert.Equal(locationsBefore + 1, await db.Locations.CountAsync());
    Assert.Single(await db.BusTerminals.Select(row => row.LocationId).Distinct().ToArrayAsync());
    var eventsBefore = await db.OutboxMessages.CountAsync();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(api, command)).StatusCode);
    Assert.Equal(eventsBefore, await db.OutboxMessages.CountAsync());
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "unrelated-member", resourceAccess: BusRoles("terminal.read", "terminal.write")));
    Assert.Empty((await client.GetFromJsonAsync<GeographyOption[]>(api + "/regions"))!);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(api, command)).StatusCode);
  }

  [SqlServerFact]
  public async Task LocationGeographyMigrationPreservesLegacyDataAndDatabaseRejectsMismatchedParents()
  {
    await using var db = CreateDatabase();
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261118032912_AddCodeDecodeGeography");
    var legacy = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Locations] ([Id], [Code], [Name], [CountryCode], [IsActive], [IsDemo]) VALUES ({legacy}, 'LEGACY', N'Legacy location', 'PH', 1, 0)");
    await migrator.MigrateAsync();
    var preserved = await db.Locations.AsNoTracking().SingleAsync();
    Assert.Equal(legacy, preserved.Id); Assert.Equal("Legacy location", preserved.Name); Assert.Null(preserved.CodeDecodeBarangay);
    var id = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO [Locations] ([Id], [Code], [Name], [CountryCode], [IsActive], [IsDemo], [CodeDecodeRegion], [CodeDecodeProvince], [CodeDecodeMunicipality], [CodeDecodeBarangay])
      VALUES ({id}, 'PH-BRG-1', N'Adams (Ilocos Norte)', 'PH', 1, 0, 3, 2, 1, 1)
      """);
    foreach (var invalid in new[] {
      "UPDATE [Locations] SET [CodeDecodeRegion]=1 WHERE [Code]='PH-BRG-1'",
      "UPDATE [Locations] SET [CodeDecodeProvince]=1 WHERE [Code]='PH-BRG-1'",
      "UPDATE [Locations] SET [CodeDecodeMunicipality]=2 WHERE [Code]='PH-BRG-1'",
      "UPDATE [Locations] SET [CodeDecodeBarangay]=2 WHERE [Code]='PH-BRG-1'",
      "UPDATE [Locations] SET [CodeDecodeBarangay]=NULL WHERE [Code]='PH-BRG-1'" })
    {
#pragma warning disable EF1002 // Fixed test SQL statements, without user input.
      var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync(invalid));
#pragma warning restore EF1002
      Assert.Equal(547, error.Number);
    }
    var duplicate = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("""
      INSERT INTO [Locations] ([Id], [Code], [Name], [CountryCode], [IsActive], [IsDemo], [CodeDecodeRegion], [CodeDecodeProvince], [CodeDecodeMunicipality], [CodeDecodeBarangay])
      VALUES (NEWID(), 'DUPLICATE', N'Adams (Ilocos Norte)', 'PH', 1, 0, 3, 2, 1, 1)
      """));
    Assert.Contains(duplicate.Number, new[] { 2601, 2627 });
    await migrator.MigrateAsync("20261118032912_AddCodeDecodeGeography");
    await migrator.MigrateAsync();
    Assert.Equal(2, await db.Locations.CountAsync());
    Assert.Equal(legacy, (await db.Locations.SingleAsync(row => row.Code == "LEGACY")).Id);
    Assert.False(db.Database.HasPendingModelChanges());
  }
}
