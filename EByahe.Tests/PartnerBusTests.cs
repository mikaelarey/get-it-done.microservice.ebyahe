using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static object BusRoles(params string[] roles) => new Dictionary<string, object> {
    ["get-it-done-web"] = new { roles = new[] { "partner" } }, ["get-it-done-ebyahe-api"] = new { roles } };

  [SqlServerFact]
  public async Task PartnerBusEndpointsRequireExactReadAndWriteClientRoles()
  {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var command = new { partnerId = partner.Id, fleetNumber = "BUS-001", plateNumber = "ABC-1234", busClass = "Deluxe", seatCapacity = 40 };
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/partner/buses")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
    foreach (var roles in new[] { BusRoles(), BusRoles("bus.write"), new Dictionary<string, object> {
      ["get-it-done-web"] = new { roles = new[] { "partner", "bus.read", "bus.write" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles, realmAccess: new { roles = new[] { "bus.read", "bus.write" } }));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/buses")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read")));
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/partner/buses")).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/buses/workspaces")).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
  }

  [SqlServerFact]
  public async Task PartnerBusesPersistValidatePaginateAndStayInsideActiveApprovedMemberships()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var foreign = new Partner { Code = "FOREIGN-BUS", DisplayName = "Foreign operator" };
    db.Buses.Add(new Bus { Partner = foreign, FleetNumber = "HIDDEN", PlateNumber = "HID-0001", BusClass = "Hidden class" });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Equal(partner.Id, Assert.Single((await client.GetFromJsonAsync<PartnerBusWorkspace[]>("/api/partner/buses/workspaces"))!).Id);
    var command = new { partnerId = partner.Id, fleetNumber = " bus-001 ", plateNumber = "abc-1234", busClass = " Deluxe ", seatCapacity = 40 };
    using var created = await client.PostAsJsonAsync("/api/partner/buses", command);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.True(created.Headers.CacheControl!.NoStore);
    var bus = (await created.Content.ReadFromJsonAsync<PartnerPortalBus>())!;
    Assert.NotEqual(Guid.Empty, bus.Id); Assert.Equal("BUS-001", bus.FleetNumber); Assert.Equal("ABC-1234", bus.PlateNumber); Assert.Equal("Deluxe", bus.BusClass);
    Assert.True(bus.IsActive); Assert.Equal(40, (await db.Buses.SingleAsync(row => row.Id == bus.Id)).SeatCapacity);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/buses", command with { plateNumber = "ABC-9999" })).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/buses", command with { fleetNumber = "BUS-002" })).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/buses", command with { fleetNumber = "BUS-002", plateNumber = "ABC-9999" })).StatusCode);
    foreach (var invalid in new[] { command with { seatCapacity = 0 }, command with { seatCapacity = 101 }, command with { plateNumber = "bad/plate" }, command with { fleetNumber = " " }, command with { busClass = " " } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/buses", invalid)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/buses", command with { partnerId = foreign.Id })).StatusCode);
    var first = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalBus>>("/api/partner/buses?pageSize=1"))!;
    var next = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalBus>>("/api/partner/buses?pageSize=1&page=2"))!;
    Assert.Equal(2, first.TotalCount); Assert.NotEqual(Assert.Single(first.Items).Id, Assert.Single(next.Items).Id);
    Assert.All(first.Items.Concat(next.Items), row => Assert.Equal(partner.Id, row.PartnerId));
    foreach (var invalid in new[] { "page=0", "pageSize=51" }) Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/partner/buses?" + invalid)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: PartnerApiFactory.Subject.ToUpperInvariant(), resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalBus>>("/api/partner/buses"))!.Items);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalBus>>("/api/partner/buses"))!.Items);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
    membership.IsActive = true; var savedPartner = await db.Partners.FindAsync(partner.Id); savedPartner!.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerBusWorkspace[]>("/api/partner/buses/workspaces"))!);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
  }
}
