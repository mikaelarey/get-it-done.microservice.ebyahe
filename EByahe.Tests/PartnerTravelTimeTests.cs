using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task TravelTimeTerminalFiltersCombineDirectionSearchAndPaginationWithoutExposingOtherPartners()
  {
    var (partner, _, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var third = new BusTerminal { PartnerId = partner.Id, LocationId = origin.LocationId, Name = "Third terminal", Address = "Third street", IsActive = false };
    var foreign = new Partner { Code = "FILTER-FOREIGN", DisplayName = "Foreign operator" };
    var foreignOrigin = new BusTerminal { Partner = foreign, LocationId = origin.LocationId, Name = "Foreign origin", Address = "Hidden" };
    var foreignDestination = new BusTerminal { Partner = foreign, LocationId = destination.LocationId, Name = "Foreign destination", Address = "Hidden" };
    db.BusTerminals.AddRange(third, foreignOrigin, foreignDestination);
    var forward = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    var reverse = new BusRoute { PartnerId = partner.Id, OriginTerminalId = destination.Id, DestinationTerminalId = origin.Id, DurationMinutes = null };
    var inactive = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminal = third, DurationMinutes = 30, IsActive = false };
    var fromThird = new BusRoute { PartnerId = partner.Id, OriginTerminal = third, DestinationTerminalId = destination.Id, DurationMinutes = 45 };
    db.BusRoutes.AddRange(forward, reverse, inactive, fromThird,
      new BusRoute { Partner = foreign, OriginTerminal = foreignOrigin, DestinationTerminal = foreignDestination, DurationMinutes = 777 });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string path = "/api/partner/travel-times";
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path + "/filter-terminals")).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read")));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "/filter-terminals")).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("travel-time.read")));
    using var choices = await client.GetAsync(path + "/filter-terminals"); Assert.True(choices.Headers.CacheControl!.NoStore);
    var terminals = (await choices.Content.ReadFromJsonAsync<PartnerTripFilterTerminal[]>())!;
    Assert.Equal(3, terminals.Length); Assert.Contains(terminals, terminal => terminal.Id == third.Id);
    Assert.DoesNotContain(terminals, terminal => terminal.Id == foreignOrigin.Id || terminal.Id == foreignDestination.Id);
    async Task<PartnerRecordsPage<PartnerTravelTime>> Rows(string query) =>
      (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerTravelTime>>(path + "?" + query))!;
    var all = await Rows(""); Assert.Equal(4, all.TotalCount);
    var fromOrigin = await Rows($"originTerminalId={origin.Id}&pageSize=1&page=2");
    Assert.Equal(2, fromOrigin.TotalCount); Assert.Equal(2, fromOrigin.Page); Assert.Single(fromOrigin.Items);
    Assert.All(fromOrigin.Items, row => Assert.Equal(origin.Id, row.OriginTerminalId));
    var toDestination = await Rows($"destinationTerminalId={destination.Id}");
    Assert.Equal(2, toDestination.TotalCount); Assert.All(toDestination.Items, row => Assert.Equal(destination.Id, row.DestinationTerminalId));
    Assert.Equal(forward.Id, Assert.Single((await Rows($"originTerminalId={origin.Id}&destinationTerminalId={destination.Id}&search=60")).Items).Id);
    Assert.Empty((await Rows($"originTerminalId={origin.Id}&destinationTerminalId={destination.Id}&search=45")).Items);
    Assert.Equal(reverse.Id, Assert.Single((await Rows($"originTerminalId={destination.Id}&destinationTerminalId={origin.Id}&search=Not%20configured")).Items).Id);
    Assert.Equal(inactive.Id, Assert.Single((await Rows($"originTerminalId={origin.Id}&destinationTerminalId={third.Id}")).Items).Id);
    Assert.Empty((await Rows($"originTerminalId={origin.Id}&destinationTerminalId={origin.Id}")).Items);
    Assert.Empty((await Rows($"originTerminalId={foreignOrigin.Id}")).Items);
    Assert.Empty((await Rows($"destinationTerminalId={foreignDestination.Id}")).Items);
    foreach (var invalid in new[] { "originTerminalId=bad", "destinationTerminalId=bad", "originTerminalId=" + Guid.Empty, "destinationTerminalId=" + Guid.Empty })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?" + invalid)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "unrelated-partner", resourceAccess: BusRoles("travel-time.read")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerTripFilterTerminal[]>(path + "/filter-terminals"))!);
    Assert.Empty((await Rows($"originTerminalId={origin.Id}&destinationTerminalId={destination.Id}")).Items);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("travel-time.read")));
    (await db.PartnerUsers.SingleAsync()).IsActive = false; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerTripFilterTerminal[]>(path + "/filter-terminals"))!);
    Assert.Empty((await Rows($"originTerminalId={origin.Id}")).Items);
  }

  [SqlServerFact]
  public async Task TravelTimeSetupPersistsSearchesAndEnforcesPartnerPermissionsAndTerminalScope()
  {
    var (partner, _, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string path = "/api/partner/travel-times";
    var command = new AddPartnerTravelTime(partner.Id, origin.Id, destination.Id, 60);
    client.DefaultRequestHeaders.Authorization = null; Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    foreach (var roles in new[] { BusRoles("travel-time.write"), BusRoles("scheduled-trip.read", "scheduled-trip.write"), BusRoles("bus.read", "bus.write"), new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "partner", "travel-time.read", "travel-time.write" } } } }) {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("travel-time.read")));
    var options = (await client.GetFromJsonAsync<PartnerTravelTimeOptions>(path + "/options"))!;
    Assert.Single(options.Workspaces); Assert.Equal(2, options.Terminals.Count);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("travel-time.read", "travel-time.write", "scheduled-trip.read", "scheduled-trip.write")));
    using var added = await client.PostAsJsonAsync(path, command); Assert.Equal(HttpStatusCode.Created, added.StatusCode);
    var route = (await added.Content.ReadFromJsonAsync<PartnerTravelTime>())!; Assert.Equal(60, route.DurationMinutes);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, command)).StatusCode);
    foreach (var invalid in new[] { command with { DurationMinutes = 0 }, command with { DurationMinutes = 2881 }, command with { DestinationTerminalId = origin.Id }, command with { DestinationTerminalId = Guid.NewGuid() } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, invalid)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, command with { PartnerId = Guid.NewGuid() })).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path + "/" + Guid.NewGuid(), new UpdatePartnerTravelTime(90, true))).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(path, command with { OriginTerminalId = destination.Id, DestinationTerminalId = origin.Id, DurationMinutes = 90 })).StatusCode);
    var rows = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerTravelTime>>(path + "?search=origin%20terminal&pageSize=1"))!;
    Assert.Equal(2, rows.TotalCount); Assert.Single(rows.Items);
    Assert.Equal(1, (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerTravelTime>>(path + "?search=90"))!.TotalCount);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path + "/" + route.Id, new UpdatePartnerTravelTime(75, false))).StatusCode);
    var saved = await db.BusRoutes.AsNoTracking().SingleAsync(row => row.Id == route.Id); Assert.Equal(75, saved.DurationMinutes); Assert.False(saved.IsActive);
    var tripOptions = (await client.GetFromJsonAsync<PartnerTripOptions>("/api/partner/scheduled-trips/options"))!; Assert.DoesNotContain(tripOptions.Routes, row => row.Id == route.Id);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path + "/" + route.Id, new UpdatePartnerTravelTime(75, true))).StatusCode);
    tripOptions = (await client.GetFromJsonAsync<PartnerTripOptions>("/api/partner/scheduled-trips/options"))!; Assert.Equal(75, tripOptions.Routes.Single(row => row.Id == route.Id).DurationMinutes);
    foreach (var query in new[] { "page=0", "pageSize=51" }) Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?" + query)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "unrelated-partner", resourceAccess: BusRoles("travel-time.read", "travel-time.write", "scheduled-trip.read", "scheduled-trip.write")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerTravelTime>>(path))!.Items);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path + "/" + route.Id, new UpdatePartnerTravelTime(80, true))).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("travel-time.read", "travel-time.write", "scheduled-trip.read", "scheduled-trip.write")));
    (await db.PartnerUsers.SingleAsync()).IsActive = false; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerTravelTime>>(path))!.Items);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, command)).StatusCode);
  }

  [SqlServerFact]
  public async Task TravelTimeChangesPreservePublishedTripsBookingDurationsAndOverlapChecks()
  {
    var trip = await SeedEditableTrip(); await using var db = CreateDatabase();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("travel-time.read", "travel-time.write", "scheduled-trip.read", "scheduled-trip.write")));
    var path = "/api/partner/travel-times/" + trip.BusRouteId;
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, new UpdatePartnerTravelTime(60, true))).StatusCode);
    var details = (await client.GetFromJsonAsync<PartnerScheduledTripDetails>("/api/partner/scheduled-trips/" + trip.Id))!;
    Assert.Equal(120, details.DurationMinutes);
    var snapshot = await db.ScheduledTrips.AsNoTracking().Include(row => row.Partner).Include(row => row.Bus).ThenInclude(bus => bus.BusClass)
      .Include(row => row.BusRoute).ThenInclude(route => route.OriginTerminal.Location)
      .Include(row => row.BusRoute).ThenInclude(route => route.DestinationTerminal.Location).SingleAsync();
    Assert.Equal(120, snapshot.DurationMinutes); Assert.Equal("2 hrs", BusTripResponse.From(snapshot).Duration);
    var command = new AddPartnerScheduledTrip(trip.PartnerId, trip.BusId, trip.BusRoute.OriginTerminalId, trip.BusRoute.DestinationTerminalId,
      trip.DepartureAtUtc.AddMinutes(130), 60, 500);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command)).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, new UpdatePartnerTravelTime(240, true))).StatusCode);
    // The second existing trip still ends at minute 190 after the first departure.
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command with { DepartureAtUtc = trip.DepartureAtUtc.AddMinutes(195), DurationMinutes = 240 })).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", command with { DepartureAtUtc = trip.DepartureAtUtc.AddMinutes(185), DurationMinutes = 240 })).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/partner/scheduled-trips/" + trip.Id, TripUpdate(trip))).StatusCode);
    Assert.Equal(120, (await client.GetFromJsonAsync<PartnerScheduledTripDetails>("/api/partner/scheduled-trips/" + trip.Id))!.DurationMinutes);
    Assert.Equal(new int?[] { 60, 120, 240 }, await db.ScheduledTrips.AsNoTracking().OrderBy(row => row.DurationMinutes).Select(row => row.DurationMinutes).ToArrayAsync());
  }
}
