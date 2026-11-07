using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private async Task<(Partner Partner, Bus Bus, BusTerminal Origin, BusTerminal Destination)> SeedPartnerTripWorkspace()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var bus = new Bus { PartnerId = partner.Id, FleetNumber = "TRIP-BUS", PlateNumber = "TRP-0001", BusClass = "Deluxe", SeatCapacity = 40 };
    var origin = new BusTerminal { PartnerId = partner.Id, Name = "Origin terminal", Address = "First street", Location = new Location { Code = "TRIP-ORIGIN", Name = "Origin city" } };
    var destination = new BusTerminal { PartnerId = partner.Id, Name = "Destination terminal", Address = "Second street", Location = new Location { Code = "TRIP-DESTINATION", Name = "Destination city" } };
    db.Buses.Add(bus); db.BusTerminals.AddRange(origin, destination); await db.SaveChangesAsync(); return (partner, bus, origin, destination);
  }

  [SqlServerFact]
  public async Task PartnerScheduledTripCreationRequiresExactBusReadAndWriteRoles()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var command = new { partnerId = partner.Id, busId = bus.Id, originTerminalId = origin.Id, destinationTerminalId = destination.Id,
      departureAtUtc = DateTimeOffset.UtcNow.AddDays(3), durationMinutes = 120, baseFare = 550m };
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/partner/scheduled-trips/options")).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command)).StatusCode);
    foreach (var roles in new[] { BusRoles(), BusRoles("bus.read"), BusRoles("bus.write"), new Dictionary<string, object> {
      ["get-it-done-web"] = new { roles = new[] { "partner", "bus.read", "bus.write" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles, realmAccess: new { roles = new[] { "bus.read", "bus.write" } }));
      Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/partner/scheduled-trips")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/scheduled-trips/options")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command)).StatusCode);
  }

  [SqlServerFact]
  public async Task PartnerScheduledTripsPersistUtcInventoryAndRoutesAndPreventOverlappingBusSchedules()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var foreign = new Partner { Code = "FOREIGN-TRIP", DisplayName = "Foreign operator" };
    var foreignBus = new Bus { Partner = foreign, FleetNumber = "FOREIGN-BUS", BusClass = "Hidden" };
    var inactiveBus = new Bus { PartnerId = partner.Id, FleetNumber = "INACTIVE-BUS", BusClass = "Hidden", IsActive = false };
    var foreignTerminal = new BusTerminal { Partner = foreign, LocationId = origin.LocationId, Name = "Foreign terminal", Address = "Hidden" };
    var inactiveTerminal = new BusTerminal { PartnerId = partner.Id, LocationId = origin.LocationId, Name = "Inactive terminal", Address = "Hidden", IsActive = false };
    db.Buses.AddRange(foreignBus, inactiveBus); db.BusTerminals.AddRange(foreignTerminal, inactiveTerminal); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var options = (await client.GetFromJsonAsync<PartnerTripOptions>("/api/partner/scheduled-trips/options"))!;
    Assert.Equal(partner.Id, Assert.Single(options.Workspaces).Id); Assert.Equal(bus.Id, Assert.Single(options.Buses).Id);
    Assert.Equal(2, options.Terminals.Count); Assert.All(options.Terminals, terminal => Assert.Equal(partner.Id, terminal.PartnerId)); Assert.Empty(options.Routes);
    var departure = DateTimeOffset.UtcNow.AddDays(3);
    var command = new { partnerId = partner.Id, busId = bus.Id, originTerminalId = origin.Id, destinationTerminalId = destination.Id,
      departureAtUtc = departure.ToOffset(TimeSpan.FromHours(8)), durationMinutes = 120, baseFare = 550.25m };
    using var created = await client.PostAsJsonAsync("/api/partner/scheduled-trips", command);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.True(created.Headers.CacheControl!.NoStore);
    var trip = (await created.Content.ReadFromJsonAsync<PartnerPortalTrip>())!;
    Assert.NotEqual(Guid.Empty, trip.Id); Assert.Equal(departure, trip.DepartureAtUtc); Assert.Equal(TimeSpan.Zero, trip.DepartureAtUtc.Offset);
    Assert.Equal(40, trip.AvailableSeats); Assert.Equal(550.25m, trip.Fare); Assert.Equal("TRP-0001", trip.PlateNumber); Assert.Equal("Scheduled", trip.Status);
    var saved = await db.ScheduledTrips.SingleAsync(); Assert.Equal(bus.Id, saved.BusId); Assert.Equal(partner.Id, saved.PartnerId);
    var route = await db.BusRoutes.SingleAsync(); Assert.Equal(route.Id, saved.BusRouteId); Assert.Equal(120, route.DurationMinutes);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command with { departureAtUtc = departure.AddMinutes(60) })).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command with {
      originTerminalId = destination.Id, destinationTerminalId = origin.Id, departureAtUtc = departure.AddMinutes(-60) })).StatusCode);
    Assert.Equal(1, await db.BusRoutes.CountAsync());
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command with { departureAtUtc = departure.AddMinutes(120) })).StatusCode);
    Assert.Equal(1, await db.BusRoutes.CountAsync());
    var concurrent = command with { departureAtUtc = departure.AddDays(7) };
    var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/partner/scheduled-trips", concurrent), client.PostAsJsonAsync("/api/partner/scheduled-trips", concurrent));
    Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created); Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
    foreach (var response in responses) response.Dispose(); Assert.Equal(3, await db.ScheduledTrips.CountAsync());
    foreach (var invalid in new[] { command with { busId = foreignBus.Id }, command with { busId = inactiveBus.Id },
      command with { originTerminalId = foreignTerminal.Id }, command with { destinationTerminalId = inactiveTerminal.Id },
      command with { destinationTerminalId = origin.Id }, command with { departureAtUtc = DateTimeOffset.UtcNow.AddDays(-1) },
      command with { durationMinutes = 0 }, command with { durationMinutes = 121 }, command with { durationMinutes = 2881 },
      command with { baseFare = 0m }, command with { baseFare = 1.234m }, command with { departureAtUtc = DateTimeOffset.MaxValue } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", invalid)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command with { partnerId = foreign.Id })).StatusCode);
    options = (await client.GetFromJsonAsync<PartnerTripOptions>("/api/partner/scheduled-trips/options"))!;
    Assert.Equal(route.Id, Assert.Single(options.Routes).Id);
    var list = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>("/api/partner/scheduled-trips?pageSize=1"))!;
    Assert.Equal(3, list.TotalCount); Assert.Single(list.Items);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: PartnerApiFactory.Subject.ToUpperInvariant(), resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerTripOptions>("/api/partner/scheduled-trips/options"))!.Buses);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerTripOptions>("/api/partner/scheduled-trips/options"))!.Workspaces);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command)).StatusCode);
  }
}
