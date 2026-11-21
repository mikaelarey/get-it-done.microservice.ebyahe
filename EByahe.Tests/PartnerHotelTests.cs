using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Application.Dtos.Requests;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static UpdatePartnerHotelRequest HotelUpdate(AddPartnerHotel command) => new() {
    PartnerId = command.PartnerId, Name = command.Name, Address = command.Address, Email = command.Email,
    ContactNumber = command.ContactNumber, Description = command.Description, CodeDecodeRegion = command.CodeDecodeRegion,
    CodeDecodeProvince = command.CodeDecodeProvince, CodeDecodeMunicipality = command.CodeDecodeMunicipality,
    CodeDecodeBarangay = command.CodeDecodeBarangay, IsActive = true };

  [SqlServerFact]
  public async Task PartnerHotelDetailsAndEditsEnforceRolesAndPersistValidUpdates()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    var created = await client.PostAsJsonAsync("/api/partner/hotels", command);
    var hotel = (await created.Content.ReadFromJsonAsync<PartnerHotel>())!; var path = "/api/partner/hotels/" + hotel.Id;
    var update = HotelUpdate(command); update.Name = "Updated Hotel"; update.Email = "updated@example.com"; update.IsActive = false;
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read")));
    Assert.Equal(hotel.Id, (await client.GetFromJsonAsync<PartnerHotel>(path))!.Id);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, update)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.write")));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, update)).StatusCode);
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(path, update)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    await using var db = CreateDatabase(); var original = await db.Hotels.AsNoTracking().SingleAsync();
    using var response = await client.PutAsJsonAsync(path, update); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(response.Headers.CacheControl!.NoStore);
    var result = (await response.Content.ReadFromJsonAsync<PartnerHotel>())!;
    Assert.Equal("Updated Hotel", result.Name); Assert.Equal("updated@example.com", result.Email); Assert.False(result.IsActive);
    var saved = await db.Hotels.AsNoTracking().SingleAsync(); Assert.Equal(original.CreatedBy, saved.CreatedBy);
    Assert.Equal(original.CreatedAtUtc, saved.CreatedAtUtc); Assert.Equal(partner.Id, saved.PartnerId);
    update.Email = ""; Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, update)).StatusCode); update.Email = command.Email;
    update.CodeDecodeBarangay = int.MaxValue; Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, update)).StatusCode);
    update.CodeDecodeBarangay = command.CodeDecodeBarangay; update.IsActive = null;
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, update)).StatusCode); update.IsActive = true;
    update.PartnerId = Guid.NewGuid(); Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, update)).StatusCode);
    update.PartnerId = partner.Id;
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/hotels", command with { Name = "Other Hotel" })).StatusCode);
    update.Name = "Other Hotel"; Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, update)).StatusCode);
    Assert.Equal("Updated Hotel", (await client.GetFromJsonAsync<PartnerHotel>(path))!.Name);
  }

  [SqlServerFact]
  public async Task PartnerHotelDetailsAndEditsHideRecordsOutsideActiveMemberships()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    var created = await client.PostAsJsonAsync("/api/partner/hotels", command);
    var hotel = (await created.Content.ReadFromJsonAsync<PartnerHotel>())!; var path = "/api/partner/hotels/" + hotel.Id;
    var update = HotelUpdate(command);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "other-hotel-user", resourceAccess: BusRoles("hotel.read", "hotel.write")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, update)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    await using var db = CreateDatabase(); var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, update)).StatusCode);
    Assert.Equal(command.Name, (await db.Hotels.SingleAsync()).Name);
  }

  private async Task<AddPartnerHotel> HotelCommand(Guid partnerId, string name = "Seaside Hotel")
  {
    await using var db = CreateDatabase();
    return await db.CodeDecodeBarangays.OrderBy(row => row.Code).Select(row => new AddPartnerHotel(partnerId,
      name, "12 Beach Street", row.Municipality.Province.CodeDecodeRegion, row.Municipality.CodeDecodeProvince,
      row.CodeDecodeMunicipality, row.Code, "+63 917 123 4567", "reservations@example.com", "Near the beach")).FirstAsync();
  }

  [SqlServerFact]
  public async Task PartnerHotelEndpointsEnforceExactClientReadAndWriteRoles()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/partner/hotels")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/partner/hotels", command)).StatusCode);
    foreach (var roles in new[] { BusRoles(), BusRoles("hotel.write"), BusRoles("room.read", "room.write"),
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "partner", "hotel.read", "hotel.write" } } },
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin" } },
        ["get-it-done-ebyahe-api"] = new { roles = new[] { "hotel.read", "hotel.write" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles, realmAccess: new { roles = new[] { "hotel.read", "hotel.write" } }));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/hotels")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/hotels", command)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read")));
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/partner/hotels")).StatusCode);
    foreach (var path in new[] { "workspaces", "regions", "provinces?codeDecodeRegion=1", "municipalities?codeDecodeProvince=1", "barangays?codeDecodeMunicipality=1" })
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/hotels/" + path)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/hotels", command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/hotels", command)).StatusCode);
  }

  [SqlServerFact]
  public async Task PartnerHotelsPersistFourAddressReferencesAndRequiredEmailWithSearchAndPagination()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    using var response = await client.PostAsJsonAsync("/api/partner/hotels", command with { Name = " Seaside Hotel ", Address = " 12 Beach Street ", Description = " Near the beach " });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var hotel = (await response.Content.ReadFromJsonAsync<PartnerHotel>())!;
    Assert.Equal("Seaside Hotel", hotel.Name); Assert.Equal("12 Beach Street", hotel.Address); Assert.True(hotel.IsActive);
    await using var db = CreateDatabase(); var saved = await db.Hotels.SingleAsync();
    Assert.Equal(command.CodeDecodeRegion, saved.CodeDecodeRegion); Assert.Equal(command.CodeDecodeProvince, saved.CodeDecodeProvince);
    Assert.Equal(command.CodeDecodeMunicipality, saved.CodeDecodeMunicipality); Assert.Equal(command.CodeDecodeBarangay, saved.CodeDecodeBarangay);
    Assert.Equal(command.Email, saved.Email); Assert.Equal(PartnerApiFactory.Subject, saved.CreatedBy);
    Assert.Equal("Near the beach", saved.Description); Assert.NotEqual(default, saved.CreatedAtUtc);
    Assert.NotEmpty(hotel.Region); Assert.NotEmpty(hotel.Province); Assert.NotEmpty(hotel.Municipality); Assert.NotEmpty(hotel.Barangay);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/hotels", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/hotels", command with { Name = "City Hotel", Email = "city@example.com" })).StatusCode);
    var first = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels?pageSize=1"))!;
    var second = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels?pageSize=1&page=2"))!;
    Assert.Equal(2, first.TotalCount); Assert.Equal("City Hotel", Assert.Single(first.Items).Name);
    Assert.NotEqual(first.Items[0].Id, Assert.Single(second.Items).Id);
    var search = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels?search=Seaside%20Beach"))!;
    Assert.Equal(hotel.Id, Assert.Single(search.Items).Id);
    var regions = (await client.GetFromJsonAsync<GeographyOption[]>("/api/partner/hotels/regions"))!;
    Assert.Contains(regions, row => row.Code == command.CodeDecodeRegion);
    Assert.Contains((await client.GetFromJsonAsync<GeographyOption[]>($"/api/partner/hotels/provinces?codeDecodeRegion={command.CodeDecodeRegion}"))!, row => row.Code == command.CodeDecodeProvince);
    Assert.Contains((await client.GetFromJsonAsync<GeographyOption[]>($"/api/partner/hotels/municipalities?codeDecodeProvince={command.CodeDecodeProvince}"))!, row => row.Code == command.CodeDecodeMunicipality);
    Assert.Contains((await client.GetFromJsonAsync<GeographyOption[]>($"/api/partner/hotels/barangays?codeDecodeMunicipality={command.CodeDecodeMunicipality}"))!, row => row.Code == command.CodeDecodeBarangay);
  }

  [SqlServerFact]
  public async Task PartnerHotelsRejectMissingEmailInvalidFieldsAndMismatchedAddressHierarchy()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    await using var db = CreateDatabase();
    var otherRegion = await db.CodeDecodeRegions.Where(row => row.Code != command.CodeDecodeRegion).Select(row => row.Code).FirstAsync();
    var otherProvince = await db.CodeDecodeProvinces.Where(row => row.Code != command.CodeDecodeProvince).Select(row => row.Code).FirstAsync();
    var otherCity = await db.CodeDecodeMunicipalities.Where(row => row.Code != command.CodeDecodeMunicipality).Select(row => row.Code).FirstAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    foreach (var invalid in new[] { command with { Email = "" }, command with { Email = "invalid" }, command with { Email = null! },
      command with { Name = " " }, command with { Name = new string('X', 201) }, command with { Address = " " }, command with { ContactNumber = "" },
      command with { PartnerId = Guid.Empty }, command with { CodeDecodeRegion = otherRegion }, command with { CodeDecodeProvince = otherProvince },
      command with { CodeDecodeMunicipality = otherCity }, command with { CodeDecodeBarangay = int.MaxValue } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/hotels", invalid)).StatusCode);
    Assert.Equal(0, await db.Hotels.CountAsync());
    foreach (var invalid in new[] { "page=0", "pageSize=51", "search=" + new string('x', 201) })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/partner/hotels?" + invalid)).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/partner/hotels/provinces")).StatusCode);
  }

  [SqlServerFact]
  public async Task PartnerHotelsScopeAllRecordsAndWritesToActiveApprovedHotelMemberships()
  {
    var first = await SeedMemberPartner("hotel");
    async Task<Partner> AnotherCompany(string category)
    {
      await using var context = CreateDatabase();
      var application = new PartnerApplication { Id = Guid.NewGuid(), CreatedBy = PartnerApiFactory.Subject, TrackingId = "APP-" + Guid.NewGuid().ToString("N"),
        Status = "Approved", BusinessCategory = category, EntityType = "corporation", SettlementFrequency = "daily",
        BusinessName = "Another company", SubmittedAtUtc = DateTimeOffset.UtcNow };
      var partner = new Partner { Code = "HOTEL-" + Guid.NewGuid().ToString("N"), DisplayName = "Another company", PartnerApplicationId = application.Id };
      context.PartnerApplications.Add(application); context.Partners.Add(partner);
      var user = await context.RefUsers.SingleAsync(row => row.ExternalUserId == PartnerApiFactory.Subject);
      context.PartnerUsers.Add(new PartnerUser { Partner = partner, RefUserId = user.Id });
      await context.SaveChangesAsync(); return partner;
    }
    var second = await AnotherCompany("hotel"); var bus = await AnotherCompany("bus-operator");
    var command = await HotelCommand(first.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    var workspaces = (await client.GetFromJsonAsync<PartnerHotelWorkspace[]>("/api/partner/hotels/workspaces"))!;
    Assert.Equal(2, workspaces.Length); Assert.DoesNotContain(workspaces, row => row.Id == bus.Id);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/hotels", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/hotels", command with { PartnerId = second.Id })).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/hotels", command with { PartnerId = bus.Id })).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/hotels", command with { PartnerId = Guid.NewGuid() })).StatusCode);
    Assert.Equal(2, (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels"))!.TotalCount);
    await using var db = CreateDatabase(); var membership = await db.PartnerUsers.SingleAsync(row => row.PartnerId == second.Id);
    membership.IsActive = false; await db.SaveChangesAsync();
    var visible = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels"))!;
    Assert.Equal(first.Id, Assert.Single(visible.Items).PartnerId);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/hotels", command with { PartnerId = second.Id })).StatusCode);
    var savedPartner = await db.Partners.SingleAsync(row => row.Id == first.Id); savedPartner.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels"))!.Items);
    Assert.Empty((await client.GetFromJsonAsync<PartnerHotelWorkspace[]>("/api/partner/hotels/workspaces"))!);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/hotels", command)).StatusCode);
  }
}
