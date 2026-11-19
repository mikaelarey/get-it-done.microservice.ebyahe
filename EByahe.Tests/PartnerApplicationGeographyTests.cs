using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ApplicationGeographyIsAvailableToApplicantsWithoutPartnerRolesAndFiltersByParent()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    await using var db = CreateDatabase();
    var first = await db.CodeDecodeBarangays.Include(row => row.Municipality).ThenInclude(row => row.Province)
      .OrderBy(row => row.Code).FirstAsync();
    var urls = new[] { "/api/partner-applications/regions",
      $"/api/partner-applications/provinces?codeDecodeRegion={first.Municipality.Province.CodeDecodeRegion}",
      $"/api/partner-applications/municipalities?codeDecodeProvince={first.Municipality.CodeDecodeProvince}",
      $"/api/partner-applications/barangays?codeDecodeMunicipality={first.CodeDecodeMunicipality}" };
    var expected = new[] {
      await db.CodeDecodeRegions.OrderBy(row => row.Decode).ThenBy(row => row.Code).Select(row => row.Code).ToArrayAsync(),
      await db.CodeDecodeProvinces.Where(row => row.CodeDecodeRegion == first.Municipality.Province.CodeDecodeRegion).OrderBy(row => row.Decode).ThenBy(row => row.Code).Select(row => row.Code).ToArrayAsync(),
      await db.CodeDecodeMunicipalities.Where(row => row.CodeDecodeProvince == first.Municipality.CodeDecodeProvince).OrderBy(row => row.Decode).ThenBy(row => row.Code).Select(row => row.Code).ToArrayAsync(),
      await db.CodeDecodeBarangays.Where(row => row.CodeDecodeMunicipality == first.CodeDecodeMunicipality).OrderBy(row => row.Decode).ThenBy(row => row.Code).Select(row => row.Code).ToArrayAsync() };
    for (var index = 0; index < urls.Length; index++)
    {
      var rows = await client.GetFromJsonAsync<GeographyOption[]>(urls[index]);
      Assert.NotNull(rows);
      Assert.Equal(expected[index], rows.Select(row => row.Code));
      Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Decode)));
    }
    Assert.Empty(await client.GetFromJsonAsync<GeographyOption[]>("/api/partner-applications/provinces?codeDecodeRegion=2147483647") ?? []);
    foreach (var url in new[] { "/api/partner-applications/provinces", "/api/partner-applications/municipalities?codeDecodeProvince=0", "/api/partner-applications/barangays?codeDecodeMunicipality=-1" })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url)).StatusCode);
    client.DefaultRequestHeaders.Authorization = null;
    foreach (var url in urls) Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
  }

  [SqlServerFact]
  public async Task ApplicationGeographyRejectsMismatchedAndUnknownCodesBeforeStoringDocuments()
  {
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    foreach (var code in new[] { 1, int.MaxValue })
    {
      using var form = ApplicationForm(omit: "corporate.codeDecodeProvince");
      form.Add(new StringContent(code.ToString()), "corporate.codeDecodeProvince");
      using var response = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.Contains("corporate.codeDecodeBarangay", await response.Content.ReadAsStringAsync());
    }
    await using var db = CreateDatabase();
    Assert.Empty(await db.PartnerApplications.ToArrayAsync());
    Assert.Empty(await db.PartnerApplicationDocuments.ToArrayAsync());
    Assert.Equal(0, factory.Storage.PutCalls);
  }

  [SqlServerFact]
  public async Task ApplicationGeographyMigrationPreservesLegacyAddressesAndEnforcesCompleteMatchingHierarchy()
  {
    await using var db = CreateDatabase();
    var legacy = ReviewApplication("Legacy geography", "hotel", "Approved", 1, "legacy-geography");
    db.PartnerApplications.Add(legacy); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261119132333_AddTopicCodeDecode");
    await migrator.MigrateAsync();
    var saved = await db.PartnerApplications.SingleAsync(row => row.Id == legacy.Id);
    Assert.Equal(legacy.Address, saved.Address); Assert.Null(saved.CodeDecodeRegion); Assert.Null(saved.CodeDecodeBarangay);
    await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
      $"UPDATE PartnerApplications SET CodeDecodeRegion=3 WHERE Id={legacy.Id}"));
    await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
      $"UPDATE PartnerApplications SET CodeDecodeRegion=3, CodeDecodeProvince=1, CodeDecodeMunicipality=1, CodeDecodeBarangay=1 WHERE Id={legacy.Id}"));
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PartnerApplications SET CodeDecodeRegion=3, CodeDecodeProvince=2, CodeDecodeMunicipality=1, CodeDecodeBarangay=1 WHERE Id={legacy.Id}");
    Assert.False(db.Database.HasPendingModelChanges());
    var address = await new PartnerApplicationRepository(db).CorporateAddressAsync(3, 2, 1, 1, "  Main Street  ");
    Assert.Equal("Main Street, Adams (Pob.), Adams, Ilocos Norte, Region I", address);
  }
}
