using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private async Task<ScheduledTrip> SeedEditableTrip()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var trip = new ScheduledTrip { PartnerId = partner.Id, BusId = bus.Id, DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(3),
      BaseFare = 500, AvailableSeats = bus.SeatCapacity,
      BusRoute = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 120 } };
    db.ScheduledTrips.Add(trip); await db.SaveChangesAsync(); return trip;
  }
  private static UpdatePartnerScheduledTrip TripUpdate(ScheduledTrip trip) => new(trip.BusId,
    trip.BusRoute.OriginTerminalId, trip.BusRoute.DestinationTerminalId, trip.DepartureAtUtc.AddDays(1), trip.BusRoute.DurationMinutes, 620.25m);

  [SqlServerFact]
  public async Task ScheduledTripUpdatesReuseRoutesExcludeThemselvesFromOverlapChecksAndSaveAuditAndNotificationsAtomically()
  {
    var trip = await SeedEditableTrip(); await using var db = CreateDatabase();
    var secondBus = new Bus { PartnerId = trip.PartnerId, FleetNumber = "OTHER", PlateNumber = "TRU-0002", CodeDecodeBusClass = 4, SeatCapacity = 50 };
    db.Buses.Add(secondBus); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    var path = "/api/partner/scheduled-trips/" + trip.Id;
    var details = (await client.GetFromJsonAsync<PartnerScheduledTripDetails>(path))!;
    Assert.True(details.CanUpdate); Assert.Null(details.UpdateRestriction); Assert.Equal("Origin terminal", details.OriginTerminal);
    Assert.Equal("Destination city", details.Destination); Assert.Equal("TRIP-BUS", details.FleetNumber); Assert.Equal(120, details.DurationMinutes);
    var command = TripUpdate(trip) with { BusId = secondBus.Id, DepartureAtUtc = trip.DepartureAtUtc.ToOffset(TimeSpan.FromHours(8)) };
    using var response = await client.PutAsJsonAsync(path, command); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var updated = (await response.Content.ReadFromJsonAsync<PartnerPortalTrip>())!;
    Assert.Equal(620.25m, updated.Fare); Assert.Equal(50, updated.AvailableSeats); Assert.Equal("TRU-0002", updated.PlateNumber);
    Assert.Equal(TimeSpan.Zero, updated.DepartureAtUtc.Offset); Assert.Equal(trip.DepartureAtUtc, updated.DepartureAtUtc);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command)).StatusCode);
    Assert.Single(await db.BusRoutes.ToArrayAsync());
    var logs = await db.ScheduledTripLogs.OrderBy(log => log.DateTimeCreated).ToArrayAsync(); Assert.Equal(2, logs.Length);
    Assert.All(logs, log => { Assert.Equal("Updated", log.Action); Assert.Equal(PartnerApiFactory.Subject, log.CreatedBy); Assert.Null(log.UploadId); });
    using var before = JsonDocument.Parse(logs[0].PreviousValue!); using var after = JsonDocument.Parse(logs[0].CurrentValue);
    Assert.Equal(trip.BusId, before.RootElement.GetProperty("busId").GetGuid()); Assert.Equal(secondBus.Id, after.RootElement.GetProperty("busId").GetGuid());
    Assert.Equal(40, before.RootElement.GetProperty("availableSeats").GetInt32()); Assert.Equal(50, after.RootElement.GetProperty("availableSeats").GetInt32());
    var blocker = new ScheduledTrip { PartnerId = trip.PartnerId, BusId = secondBus.Id, BusRouteId = trip.BusRouteId,
      DepartureAtUtc = trip.DepartureAtUtc.AddDays(1), BaseFare = 500, AvailableSeats = 50 };
    db.ScheduledTrips.Add(blocker); await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, command with { DepartureAtUtc = blocker.DepartureAtUtc.AddMinutes(30) })).StatusCode);
    foreach (var invalid in new[] { command with { DurationMinutes = 0 }, command with { DurationMinutes = 121 }, command with { BaseFare = 1.234m },
      command with { BusId = Guid.NewGuid() }, command with { DestinationTerminalId = command.OriginTerminalId },
      command with { DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(-1) }, command with { DepartureAtUtc = DateTimeOffset.MaxValue } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, invalid)).StatusCode);
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTripUpdate ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted WHERE EventType='scheduled-trip.updated') THROW 51065,'Test outbox failure',1;");
    Assert.Equal(HttpStatusCode.InternalServerError, (await client.PutAsJsonAsync(path, command with { BaseFare = 700 })).StatusCode);
    Assert.Equal(620.25m, (await db.ScheduledTrips.AsNoTracking().SingleAsync(row => row.Id == trip.Id)).BaseFare);
    Assert.Equal(2, await db.ScheduledTripLogs.CountAsync()); Assert.Equal(2, await db.OutboxMessages.CountAsync(row => row.EventType == "scheduled-trip.updated"));
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectTripUpdate");
    // Changing direction creates a new route without changing the shared original route.
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command with { OriginTerminalId = command.DestinationTerminalId,
      DestinationTerminalId = command.OriginTerminalId, DurationMinutes = 180 })).StatusCode);
    Assert.Equal(2, await db.BusRoutes.CountAsync()); Assert.Equal(120, (await db.BusRoutes.SingleAsync(row => row.Id == trip.BusRouteId)).DurationMinutes);
  }

  [SqlServerFact]
  public async Task ScheduledTripUpdatesProtectBookingsAndExistingSeatInventoryAndRejectDepartedOrCancelledTrips()
  {
    var trip = await SeedEditableTrip(); await using var db = CreateDatabase();
    var booking = new BusBooking { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), ScheduledTripId = trip.Id, CreatedBy = PartnerApiFactory.Subject,
      Reference = "BK-UPDATE-LOCK", CreatedAtUtc = DateTimeOffset.UtcNow, Total = 500, RequestHash = "test", DetailsJson = "{}" };
    db.BusBookings.Add(booking); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    var path = "/api/partner/scheduled-trips/" + trip.Id; var command = TripUpdate(trip);
    foreach (var payment in new[] { "Pending Payment", "Payment Failed", "Payment Success" })
    {
      booking.PaymentStatus = payment; await db.SaveChangesAsync();
      var detail = (await client.GetFromJsonAsync<PartnerScheduledTripDetails>(path))!;
      Assert.False(detail.CanUpdate); Assert.Contains("active bookings", detail.UpdateRestriction);
      Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, command)).StatusCode);
    }
    booking.Status = "Cancelled"; booking.PaymentStatus = "Cancelled"; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, command)).StatusCode);
    var saved = await db.ScheduledTrips.SingleAsync(row => row.Id == trip.Id); saved.AvailableSeats = 30; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, command)).StatusCode);
    saved.AvailableSeats = 40; saved.DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(-1); await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, command)).StatusCode);
    saved.DepartureAtUtc = command.DepartureAtUtc; saved.Status = "Cancelled"; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path, command)).StatusCode);
    Assert.Single(await db.ScheduledTripLogs.ToArrayAsync());
  }

  [SqlServerFact]
  public async Task ScheduledTripDetailsAndUpdatesEnforceExactPermissionsAndCurrentPartnerMembership()
  {
    var trip = await SeedEditableTrip(); await using var db = CreateDatabase();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var path = "/api/partner/scheduled-trips/" + trip.Id; var command = TripUpdate(trip);
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync(path, command)).StatusCode);
    foreach (var roles in new[] { BusRoles("bus.read", "bus.write"), BusRoles("scheduled-trip.write") })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, command)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read")));
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "foreign-user", resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/partner/scheduled-trips/" + Guid.NewGuid())).StatusCode);
    // Partner is derived from the saved record; an extra payload property cannot transfer the trip.
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, new { command.BusId, command.OriginTerminalId, command.DestinationTerminalId,
      command.DepartureAtUtc, command.DurationMinutes, command.BaseFare, partnerId = Guid.NewGuid() })).StatusCode);
    Assert.Equal(trip.PartnerId, (await db.ScheduledTrips.AsNoTracking().SingleAsync(row => row.Id == trip.Id)).PartnerId);
    var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
    member.IsActive = true; var partner = (await db.Partners.FindAsync(trip.PartnerId))!; partner.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, command)).StatusCode);
  }
}
