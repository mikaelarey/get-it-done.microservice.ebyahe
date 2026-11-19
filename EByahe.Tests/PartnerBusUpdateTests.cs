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
  public async Task BusUpdatesNormalizeFieldsEnforceUniquenessAndRollBackNotificationsWithTheBus()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var bus = new Bus { PartnerId = partner.Id, FleetNumber = "BUS-1", PlateNumber = "ABC-0001", CodeDecodeBusClass = 2 };
    var other = new Bus { PartnerId = partner.Id, FleetNumber = "BUS-2", PlateNumber = "ABC-0002", CodeDecodeBusClass = 2 };
    db.Buses.AddRange(bus, other, new Bus { Partner = new Partner { Code = "FOREIGN-PLATE", DisplayName = "Foreign" }, FleetNumber = "BUS-1", PlateNumber = "FRN-0001", CodeDecodeBusClass = 2 });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var path = "/api/partner/buses/" + bus.Id;
    var editable = (await client.GetFromJsonAsync<PartnerBusForUpdate>(path))!;
    Assert.Equal(bus.PartnerId, editable.PartnerId); Assert.Equal(bus.PlateNumber, editable.PlateNumber); Assert.False(editable.HasUpcomingTrips);
    var command = new { fleetNumber = " updated-1 ", plateNumber = "upd-0001", busClass = "Luxury", seatCapacity = 50, isActive = false };
    using var response = await client.PutAsJsonAsync(path, command); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var updated = (await response.Content.ReadFromJsonAsync<PartnerPortalBus>())!;
    Assert.Equal("UPDATED-1", updated.FleetNumber); Assert.Equal("UPD-0001", updated.PlateNumber); Assert.Equal("Luxury", updated.BusClass);
    Assert.Equal(50, updated.SeatCapacity); Assert.False(updated.IsActive); Assert.Equal(partner.Id, updated.PartnerId);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command)).StatusCode);
    var audit = await db.BusLogs.Where(row => row.BusId == bus.Id).OrderBy(row => row.DateTimeCreated).ToArrayAsync(); Assert.Equal(2, audit.Length);
    using var before = JsonDocument.Parse(audit[0].PreviousValue!); using var after = JsonDocument.Parse(audit[0].CurrentValue);
    Assert.Equal("BUS-1", before.RootElement.GetProperty("fleetNumber").GetString()); Assert.Equal("UPDATED-1", after.RootElement.GetProperty("fleetNumber").GetString());
    Assert.Equal(2, before.RootElement.GetProperty("codeDecodeBusClass").GetInt32()); Assert.Equal(4, after.RootElement.GetProperty("codeDecodeBusClass").GetInt32());
    Assert.All(audit, row => { Assert.Equal(PartnerApiFactory.Subject, row.CreatedBy); Assert.Equal("Updated", row.Action); Assert.Null(row.UploadId); });
    var events = await db.OutboxMessages.CountAsync();
    foreach (var conflict in new[] { command with { plateNumber = "abc-0002" }, command with { plateNumber = "frn-0001" }, command with { fleetNumber = "bus-2" } })
      Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, conflict)).StatusCode);
    foreach (var invalid in new[] { command with { busClass = "Unknown class" }, command with { seatCapacity = 0 }, command with { seatCapacity = 101 },
      command with { fleetNumber = " " }, command with { fleetNumber = "Bad\nvalue" }, command with { plateNumber = "Bad!" } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, invalid)).StatusCode);
    Assert.Equal(events, await db.OutboxMessages.CountAsync());
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectBusUpdate ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.CodeDecodeEventTypes et ON et.Code=i.CodeDecodeEventType WHERE et.Decode='bus.updated') THROW 51064,'Test outbox failure',1;");
    Assert.Equal(HttpStatusCode.InternalServerError, (await client.PutAsJsonAsync(path, command with { fleetNumber = "ROLLBACK", seatCapacity = 60 })).StatusCode);
    var saved = await db.Buses.AsNoTracking().SingleAsync(row => row.Id == bus.Id);
    Assert.Equal("UPDATED-1", saved.FleetNumber); Assert.Equal(50, saved.SeatCapacity); Assert.Equal(events, await db.OutboxMessages.CountAsync());
    Assert.Equal(2, await db.BusLogs.CountAsync());
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectBusUpdate");
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command with { fleetNumber = "RETRIED" })).StatusCode);
    Assert.Equal(events + 1, await db.OutboxMessages.CountAsync());
    Assert.All(await db.OutboxMessages.Where(row => row.EventType == "bus.updated").ToArrayAsync(), activity => Assert.Equal(PartnerApiFactory.Subject, activity.UserId));
    Assert.Equal("BUS-2", (await db.Buses.AsNoTracking().SingleAsync(row => row.Id == other.Id)).FleetNumber);
  }

  [SqlServerFact]
  public async Task BusUpdatesRequireBusWriteAndActiveApprovedOperatorMembershipAndCannotTransferPartners()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var bus = new Bus { PartnerId = partner.Id, FleetNumber = "BUS-1", PlateNumber = "ABC-0001", CodeDecodeBusClass = 2 };
    var foreign = new Bus { Partner = new Partner { Code = "FOREIGN-UPDATE", DisplayName = "Foreign" }, FleetNumber = "FOREIGN", PlateNumber = "FRN-0001", CodeDecodeBusClass = 2 };
    db.Buses.AddRange(bus, foreign); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var path = "/api/partner/buses/" + bus.Id;
    var command = new { fleetNumber = "UPDATED", plateNumber = "UPD-0001", busClass = "Deluxe", seatCapacity = 40, isActive = true, partnerId = foreign.PartnerId };
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(path, command)).StatusCode);
    foreach (var roles in new[] { BusRoles("bus.read"), BusRoles("bus.write"), BusRoles("terminal.read", "terminal.write") })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, command)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command)).StatusCode);
    Assert.Equal(partner.Id, (await db.Buses.AsNoTracking().SingleAsync(row => row.Id == bus.Id)).PartnerId);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/partner/buses/" + foreign.Id)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/partner/buses/" + foreign.Id, command)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/partner/buses/" + Guid.NewGuid(), command)).StatusCode);
    var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
    member.IsActive = true; var owner = (await db.Partners.FindAsync(partner.Id))!; owner.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
    owner.Status = "Approved"; (await db.PartnerApplications.FindAsync(owner.PartnerApplicationId))!.BusinessCategory = "hotel"; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
    Assert.Equal("FOREIGN", (await db.Buses.AsNoTracking().SingleAsync(row => row.Id == foreign.Id)).FleetNumber);
  }

  [SqlServerFact]
  public async Task BusUpdatesKeepUpcomingTripInventoryStableWhileAllowingOtherBusDetailsToChange()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var trip = new ScheduledTrip { PartnerId = partner.Id, BusId = bus.Id, DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(3), AvailableSeats = 25, BaseFare = 500,
      BusRoute = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 } };
    db.ScheduledTrips.Add(trip); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var path = "/api/partner/buses/" + bus.Id;
    Assert.True((await client.GetFromJsonAsync<PartnerBusForUpdate>(path))!.HasUpcomingTrips);
    var command = new { fleetNumber = bus.FleetNumber, plateNumber = bus.PlateNumber, busClass = "Deluxe", seatCapacity = bus.SeatCapacity + 1, isActive = true };
    using var conflict = await client.PutAsJsonAsync(path, command); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    Assert.Contains("upcoming scheduled trips", await conflict.Content.ReadAsStringAsync());
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command with { seatCapacity = bus.SeatCapacity, plateNumber = "NEW-0001" })).StatusCode);
    Assert.Equal(25, (await db.ScheduledTrips.AsNoTracking().SingleAsync(row => row.Id == trip.Id)).AvailableSeats);
    trip.Status = "Cancelled"; await db.SaveChangesAsync();
    Assert.False((await client.GetFromJsonAsync<PartnerBusForUpdate>(path))!.HasUpcomingTrips);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command)).StatusCode);
  }
}
