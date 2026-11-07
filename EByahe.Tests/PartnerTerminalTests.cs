using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PartnerTerminalCreationRequiresExactBusRolesWhileTheListRemainsReadableToPartners()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var location = new Location { Code = "TERMINAL-CITY", Name = "Terminal city" }; db.Locations.Add(location); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var command = new { partnerId = partner.Id, locationId = location.Id, name = "New terminal", address = "Terminal address" };
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/partner/terminals/locations")).StatusCode);
    foreach (var roles in new[] { BusRoles(), BusRoles("bus.read"), BusRoles("bus.write"), new Dictionary<string, object> {
      ["get-it-done-web"] = new { roles = new[] { "partner", "bus.read", "bus.write" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles, realmAccess: new { roles = new[] { "bus.read", "bus.write" } }));
      Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/partner/terminals")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/terminals/workspaces")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/terminals/locations")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
  }

  [SqlServerFact]
  public async Task PartnerTerminalsUseActiveDatabaseLocationsPersistAndRejectDuplicatesAndForeignPartners()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var first = new Location { Code = "FIRST-CITY", Name = "First city" };
    var second = new Location { Code = "SECOND-CITY", Name = "Second city" };
    var inactive = new Location { Code = "INACTIVE-CITY", Name = "Inactive city", IsActive = false };
    db.Locations.AddRange(first, second, inactive);
    var foreign = new Partner { Code = "FOREIGN-TERMINAL", DisplayName = "Foreign operator" };
    db.BusTerminals.Add(new BusTerminal { Partner = foreign, Location = first, Name = "Hidden terminal", Address = "Hidden address" }); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Equal(partner.Id, Assert.Single((await client.GetFromJsonAsync<PartnerTerminalWorkspace[]>("/api/partner/terminals/workspaces"))!).Id);
    var locations = (await client.GetFromJsonAsync<PartnerTerminalLocation[]>("/api/partner/terminals/locations"))!;
    Assert.Equal(new[] { first.Id, second.Id }, locations.Select(location => location.Id));
    var command = new { partnerId = partner.Id, locationId = first.Id, name = " New terminal ", address = " First street\nCity centre " };
    using var created = await client.PostAsJsonAsync("/api/partner/terminals", command);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.True(created.Headers.CacheControl!.NoStore);
    var terminal = (await created.Content.ReadFromJsonAsync<PartnerPortalTerminal>())!;
    Assert.NotEqual(Guid.Empty, terminal.Id); Assert.Equal("New terminal", terminal.Name); Assert.Equal("First street\nCity centre", terminal.Address);
    Assert.Equal(first.Name, terminal.Location); Assert.True(terminal.IsActive);
    var saved = await db.BusTerminals.SingleAsync(row => row.Id == terminal.Id); Assert.Equal(first.Id, saved.LocationId); Assert.Equal(partner.Id, saved.PartnerId);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/terminals", command with { name = "NEW TERMINAL" })).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/terminals", command with { locationId = second.Id })).StatusCode);
    foreach (var invalid in new[] { command with { locationId = Guid.NewGuid() }, command with { locationId = inactive.Id }, command with { locationId = Guid.Empty },
      command with { name = " " }, command with { address = " " }, command with { name = new string('A', 255) }, command with { address = new string('A', 501) } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/terminals", invalid)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/terminals", command with { partnerId = foreign.Id })).StatusCode);
    var page = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTerminal>>("/api/partner/terminals?pageSize=1"))!;
    var next = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTerminal>>("/api/partner/terminals?pageSize=1&page=2"))!;
    Assert.Equal(2, page.TotalCount); Assert.NotEqual(Assert.Single(page.Items).Id, Assert.Single(next.Items).Id);
    Assert.All(page.Items.Concat(next.Items), row => Assert.Equal(partner.DisplayName, row.Partner));
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: PartnerApiFactory.Subject.ToUpperInvariant(), resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerTerminalWorkspace[]>("/api/partner/terminals/workspaces"))!);
    Assert.Empty((await client.GetFromJsonAsync<PartnerTerminalLocation[]>("/api/partner/terminals/locations"))!);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
    membership.IsActive = true; var savedPartner = await db.Partners.FindAsync(partner.Id); savedPartner!.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTerminal>>("/api/partner/terminals"))!.Items);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
    savedPartner.Status = "Approved"; var application = await db.PartnerApplications.FindAsync(savedPartner.PartnerApplicationId); application!.BusinessCategory = "hotel"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerTerminalWorkspace[]>("/api/partner/terminals/workspaces"))!);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
  }
}
