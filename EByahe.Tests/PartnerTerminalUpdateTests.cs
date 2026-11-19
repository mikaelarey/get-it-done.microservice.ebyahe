using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task TerminalUpdatesPrefillRawAddressValidateGeographyAndKeepOtherTerminalsUnchanged()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    const string api = "/api/partner/terminals";
    var create = new { partnerId = partner.Id, codeDecodeRegion = 3, codeDecodeProvince = 2, codeDecodeMunicipality = 1,
      codeDecodeBarangay = 1, name = "Original terminal", address = "Original street" };
    var original = (await (await client.PostAsJsonAsync(api, create)).Content.ReadFromJsonAsync<PartnerPortalTerminal>())!;
    var other = (await (await client.PostAsJsonAsync(api, create with { name = "Shared terminal" })).Content.ReadFromJsonAsync<PartnerPortalTerminal>())!;
    var originalLocation = await db.BusTerminals.Where(row => row.Id == other.Id).Select(row => row.LocationId).SingleAsync();
    using var get = await client.GetAsync(api + "/" + original.Id); Assert.True(get.Headers.CacheControl!.NoStore);
    var editable = (await get.Content.ReadFromJsonAsync<PartnerTerminalForUpdate>())!;
    Assert.Equal("Original street", editable.Address); Assert.Equal(partner.Id, editable.PartnerId); Assert.Equal(1, editable.CodeDecodeBarangay);
    var command = new { name = " Updated terminal ", address = " New street ", isActive = false,
      codeDecodeRegion = (int?)3, codeDecodeProvince = (int?)2, codeDecodeMunicipality = (int?)2, codeDecodeBarangay = (int?)2 };
    using var response = await client.PutAsJsonAsync(api + "/" + original.Id, command); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var updated = (await response.Content.ReadFromJsonAsync<PartnerPortalTerminal>())!;
    Assert.Equal("Updated terminal", updated.Name); Assert.False(updated.IsActive); Assert.Equal("Bacarra (Ilocos Norte)", updated.Location);
    Assert.Equal("New street, Bani, Bacarra, Ilocos Norte, Region I", updated.Address);
    // Saving unchanged details is valid; the unique index must not conflict with the record itself.
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(api + "/" + original.Id, command)).StatusCode);
    var createdLog = await db.TerminalLogs.SingleAsync(row => row.TerminalId == original.Id && row.Action == "Created"); Assert.Null(createdLog.PreviousValue);
    var updateLogs = await db.TerminalLogs.Where(row => row.TerminalId == original.Id && row.Action == "Updated").OrderBy(row => row.DateTimeCreated).ToArrayAsync(); Assert.Equal(2, updateLogs.Length);
    using var before = JsonDocument.Parse(updateLogs[0].PreviousValue!); using var after = JsonDocument.Parse(updateLogs[0].CurrentValue);
    Assert.Equal("Original street", before.RootElement.GetProperty("address").GetString()); Assert.Equal("New street", after.RootElement.GetProperty("address").GetString());
    Assert.Equal(1, before.RootElement.GetProperty("codeDecodeBarangay").GetInt32()); Assert.Equal(2, after.RootElement.GetProperty("codeDecodeBarangay").GetInt32());
    Assert.All(updateLogs, log => Assert.Equal(PartnerApiFactory.Subject, log.CreatedBy));
    var shared = await db.BusTerminals.AsNoTracking().SingleAsync(row => row.Id == other.Id);
    Assert.Equal("Shared terminal", shared.Name); Assert.Equal("Original street", shared.Address); Assert.Equal(originalLocation, shared.LocationId);
    var saved = await db.BusTerminals.AsNoTracking().SingleAsync(row => row.Id == original.Id);
    Assert.Equal(partner.Id, saved.PartnerId); Assert.Equal("New street", saved.Address); Assert.NotEqual(originalLocation, saved.LocationId);
    var events = await db.OutboxMessages.CountAsync();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(api + "/" + other.Id, command with { name = "updated TERMINAL" })).StatusCode);
    foreach (var invalid in new[] { command with { codeDecodeRegion = 1 }, command with { codeDecodeMunicipality = 1 },
      command with { codeDecodeBarangay = int.MaxValue }, command with { codeDecodeProvince = null },
      command with { codeDecodeRegion = null, codeDecodeProvince = null, codeDecodeMunicipality = null, codeDecodeBarangay = null },
      command with { name = " " }, command with { name = "Bad\nname" }, command with { address = " " } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(api + "/" + original.Id, invalid)).StatusCode);
    Assert.Equal(events, await db.OutboxMessages.CountAsync());
    Assert.Equal("Shared terminal", (await db.BusTerminals.AsNoTracking().SingleAsync(row => row.Id == other.Id)).Name);
    var location = await db.Locations.SingleAsync(row => row.CodeDecodeBarangay == 2); location.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(api + "/" + original.Id, command)).StatusCode);
    using var cors = new HttpRequestMessage(HttpMethod.Options, api + "/" + original.Id);
    cors.Headers.Add("Origin", "http://localhost:4204"); cors.Headers.Add("Access-Control-Request-Method", "PUT"); cors.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
    using var preflight = await client.SendAsync(cors); Assert.Contains("PUT", string.Join(",", preflight.Headers.GetValues("Access-Control-Allow-Methods")));
  }

  [SqlServerFact]
  public async Task TerminalUpdatesPreserveLegacyLocationsAndRequireWriteAccessToTheSelectedPartner()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var legacy = new BusTerminal { PartnerId = partner.Id, Name = "Legacy", Address = "Old street", Location = new Location { Code = "LEGACY-UPDATE", Name = "Laoag" } };
    var foreign = new BusTerminal { Partner = new Partner { Code = "FOREIGN-UPDATE", DisplayName = "Other partner" },
      Name = "Foreign terminal", Address = "Private street", Location = legacy.Location };
    db.BusTerminals.AddRange(legacy, foreign); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var path = "/api/partner/terminals/" + legacy.Id;
    var command = new { name = "Renamed legacy", address = "Updated legacy street", isActive = true };
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(path, command)).StatusCode);
    foreach (var roles in new[] { BusRoles("terminal.read"), BusRoles("terminal.write"), BusRoles("bus.read", "bus.write") })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, command)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    var details = (await client.GetFromJsonAsync<PartnerTerminalForUpdate>(path))!; Assert.Null(details.CodeDecodeBarangay);
    using var updated = await client.PutAsJsonAsync(path, command); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    Assert.Equal("Laoag", (await updated.Content.ReadFromJsonAsync<PartnerPortalTerminal>())!.Location);
    Assert.Equal(legacy.LocationId, (await db.BusTerminals.AsNoTracking().SingleAsync(row => row.Id == legacy.Id)).LocationId);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/partner/terminals/" + foreign.Id)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/partner/terminals/" + foreign.Id, command)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/partner/terminals/" + Guid.NewGuid(), command)).StatusCode);
    var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
    membership.IsActive = true; (await db.Partners.FindAsync(partner.Id))!.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
    Assert.Equal("Foreign terminal", (await db.BusTerminals.AsNoTracking().SingleAsync(row => row.Id == foreign.Id)).Name);
  }

  [SqlServerFact]
  public async Task TerminalUpdatesRollBackTerminalNewLocationAndNotificationTogether()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var terminal = new BusTerminal { PartnerId = partner.Id, Name = "Original", Address = "Old street", Location = new Location { Code = "ATOMIC-UPDATE", Name = "Old location" } };
    db.BusTerminals.Add(terminal); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    var path = "/api/partner/terminals/" + terminal.Id;
    var command = new { name = "Updated", address = "New street", isActive = false, codeDecodeRegion = 3,
      codeDecodeProvince = 2, codeDecodeMunicipality = 2, codeDecodeBarangay = 2 };
    var locations = await db.Locations.CountAsync(); var events = await db.OutboxMessages.CountAsync();
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTerminalUpdate ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.CodeDecodeEventTypes et ON et.Code=i.CodeDecodeEventType WHERE et.Decode='terminal.updated') THROW 51063,'Test outbox failure',1;");
    Assert.Equal(HttpStatusCode.InternalServerError, (await client.PutAsJsonAsync(path, command)).StatusCode);
    var unchanged = await db.BusTerminals.AsNoTracking().SingleAsync(row => row.Id == terminal.Id);
    Assert.Equal("Original", unchanged.Name); Assert.True(unchanged.IsActive); Assert.Equal(terminal.LocationId, unchanged.LocationId);
    Assert.Equal(locations, await db.Locations.CountAsync()); Assert.Equal(events, await db.OutboxMessages.CountAsync());
    Assert.Empty(await db.TerminalLogs.ToArrayAsync());
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectTerminalUpdate");
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command)).StatusCode);
    Assert.Equal(locations + 1, await db.Locations.CountAsync()); Assert.Equal(events + 1, await db.OutboxMessages.CountAsync());
    var activity = await db.OutboxMessages.SingleAsync(row => row.EventType == "terminal.updated");
    Assert.Equal(PartnerApiFactory.Subject, activity.UserId); Assert.DoesNotContain("New street", activity.Payload);
  }
}
