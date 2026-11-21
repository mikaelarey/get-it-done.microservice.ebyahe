using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ScheduledTripFiltersUseManilaHourlyRangesIndependentEndpointsAndScopedDatabasePaginationForReaders()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime);
    var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
    var route = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    ScheduledTrip Trip(BusRoute path, DateTimeOffset departure) => new() { PartnerId = partner.Id, BusId = bus.Id, BusRoute = path, DepartureAtUtc = departure, AvailableSeats = 40, BaseFare = 500 };
    for (var i = 0; i < 12; i++) db.ScheduledTrips.Add(Trip(route, start.AddMinutes(i)));
    db.ScheduledTrips.AddRange(Trip(route, start.AddTicks(-10)), Trip(route, start.AddDays(1)));
    db.ScheduledTrips.AddRange(Trip(route, start.AddHours(7)), Trip(route, start.AddHours(7).AddMinutes(30)),
      Trip(route, start.AddHours(8).AddTicks(-1)), Trip(route, start.AddHours(8)), Trip(route, start.AddHours(12)),
      Trip(route, start.AddDays(1).AddTicks(-1)));
    var reverse = new BusRoute { PartnerId = partner.Id, OriginTerminalId = destination.Id, DestinationTerminalId = origin.Id, DurationMinutes = 60 };
    db.ScheduledTrips.AddRange(Trip(reverse, start.AddHours(7).AddMinutes(15)), Trip(reverse, start.AddHours(23)));
    var other = new BusTerminal { PartnerId = partner.Id, Name = "Other terminal", LocationId = origin.LocationId, IsActive = false };
    var otherRoute = new BusRoute { PartnerId = partner.Id, OriginTerminal = other, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    db.ScheduledTrips.Add(Trip(otherRoute, start.AddHours(10)));
    db.BusTerminals.Add(new BusTerminal { PartnerId = partner.Id, Name = "Unused terminal", LocationId = origin.LocationId });
    // Different stored offsets must still produce the same Manila hour bucket.
    db.ScheduledTrips.AddRange(Trip(reverse, start.AddDays(2).AddHours(14).AddMinutes(30).ToOffset(TimeSpan.FromHours(8))),
      Trip(reverse, start.AddDays(2).AddHours(14).AddMinutes(45)));
    var foreign = new Partner { Code = "FILTER-FOREIGN", DisplayName = "Foreign company", Status = "Approved" };
    var foreignTerminal = new BusTerminal { Partner = foreign, Name = "Hidden terminal", LocationId = origin.LocationId };
    var foreignDestination = new BusTerminal { Partner = foreign, Name = "Hidden destination", LocationId = destination.LocationId };
    db.ScheduledTrips.Add(new ScheduledTrip { Partner = foreign, Bus = new Bus { Partner = foreign, FleetNumber = "HIDDEN", CodeDecodeBusClass = 2 },
      BusRoute = new BusRoute { Partner = foreign, OriginTerminal = foreignTerminal, DestinationTerminal = foreignDestination, DurationMinutes = 60 },
      DepartureAtUtc = start.AddHours(7), AvailableSeats = 40, BaseFare = 500 });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string api = "/api/partner/scheduled-trips";
    foreach (var roles in new object?[] { null, BusRoles("bus.read"), BusRoles("scheduled-trip.write") })
    {
      client.DefaultRequestHeaders.Authorization = roles is null ? null : new("Bearer", factory.Token(resourceAccess: roles));
      var status = roles is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
      Assert.Equal(status, (await client.GetAsync(api)).StatusCode); Assert.Equal(status, (await client.GetAsync(api + "/filter-options")).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read")));
    var choices = (await client.GetFromJsonAsync<PartnerTripFilterOptions>(api + "/filter-options"))!;
    var terminals = choices.Origins;
    Assert.Equal(3, terminals.Count); Assert.Contains(terminals, terminal => terminal.Id == other.Id); Assert.DoesNotContain(terminals, terminal => terminal.Id == foreignTerminal.Id);
    Assert.All(terminals, terminal => Assert.Equal(partner.DisplayName, terminal.Partner));
    Assert.Equal(new[] { "00:00", "07:00", "08:00", "10:00", "12:00", "23:00" }, choices.Times);
    Assert.Equal(2, choices.Destinations.Count); Assert.DoesNotContain(choices.Destinations, terminal => terminal.Id == other.Id);
    var tomorrowChoices = (await client.GetFromJsonAsync<PartnerTripFilterOptions>($"{api}/filter-options?date={date.AddDays(1):yyyy-MM-dd}"))!;
    Assert.Equal(new[] { "00:00" }, tomorrowChoices.Times); Assert.Equal(origin.Id, Assert.Single(tomorrowChoices.Origins).Id);
    Assert.Equal(destination.Id, Assert.Single(tomorrowChoices.Destinations).Id);
    using var changedDate = await client.GetAsync($"{api}/filter-options?date={date.AddDays(2):yyyy-MM-dd}"); Assert.True(changedDate.Headers.CacheControl!.NoStore);
    var changedChoices = (await changedDate.Content.ReadFromJsonAsync<PartnerTripFilterOptions>())!;
    Assert.Equal(new[] { "14:00" }, changedChoices.Times); Assert.Equal(destination.Id, Assert.Single(changedChoices.Origins).Id);
    Assert.Equal(origin.Id, Assert.Single(changedChoices.Destinations).Id);
    var noChoices = (await client.GetFromJsonAsync<PartnerTripFilterOptions>($"{api}/filter-options?date={date.AddDays(3):yyyy-MM-dd}"))!;
    Assert.Empty(noChoices.Times); Assert.Empty(noChoices.Origins); Assert.Empty(noChoices.Destinations);
    foreach (var invalid in new[] { "bad", "2026-02-30", "0001-01-01", "9999-12-31" })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(api + "/filter-options?date=" + invalid)).StatusCode);
    var today = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>(api + "?pageSize=5"))!;
    Assert.Equal(21, today.TotalCount); Assert.Equal(5, today.Items.Count); Assert.Equal(start, today.Items[0].DepartureAtUtc);
    Assert.All(today.Items, trip => Assert.InRange(trip.DepartureAtUtc, start, start.AddDays(1).AddTicks(-1)));
    var final = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date:yyyy-MM-dd}&originTerminalId={origin.Id}&pageSize=5&page=4"))!;
    Assert.Equal(18, final.TotalCount); Assert.Equal(3, final.Items.Count);
    var byDestination = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date:yyyy-MM-dd}&destinationTerminalId={destination.Id}"))!;
    Assert.Equal(19, byDestination.TotalCount);
    var onlyOther = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date:yyyy-MM-dd}&originTerminalId={other.Id}"))!;
    Assert.Equal(1, onlyOther.TotalCount);
    var tomorrow = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date.AddDays(1):yyyy-MM-dd}"))!; Assert.Single(tomorrow.Items);
    var yesterday = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date.AddDays(-1):yyyy-MM-dd}"))!; Assert.Single(yesterday.Items);
    async Task<PartnerRecordsPage<PartnerPortalTrip>> Rows(string query) =>
      (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date:yyyy-MM-dd}&{query}"))!;
    var morning = await Rows($"time=07:00&originTerminalId={origin.Id}&destinationTerminalId={destination.Id}&pageSize=2&page=2");
    Assert.Equal(3, morning.TotalCount); Assert.Single(morning.Items); Assert.Equal(start.AddHours(8).AddTicks(-1), morning.Items[0].DepartureAtUtc);
    Assert.Equal(4, (await Rows("time=07:00")).TotalCount);
    Assert.Equal(12, (await Rows("time=00:00")).TotalCount); // UTC date differs from the selected Manila date.
    Assert.Equal(start.AddHours(8), Assert.Single((await Rows("time=08:00")).Items).DepartureAtUtc);
    Assert.Equal(start.AddHours(12), Assert.Single((await Rows("time=12:00")).Items).DepartureAtUtc);
    Assert.Equal(2, (await Rows("time=23:00")).TotalCount);
    Assert.Equal(2, (await Rows($"originTerminalId={destination.Id}&destinationTerminalId={origin.Id}")).TotalCount);
    Assert.Empty((await Rows($"originTerminalId={origin.Id}&destinationTerminalId={origin.Id}")).Items);
    Assert.Empty((await Rows($"originTerminalId={foreignTerminal.Id}")).Items);
    Assert.Empty((await Rows($"destinationTerminalId={foreignDestination.Id}")).Items);
    Assert.Single((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date.AddDays(1):yyyy-MM-dd}&time=00:00"))!.Items);
    foreach (var query in new[] { "date=bad", "date=2026-02-30", "date=0001-01-01", "date=9999-12-31", "originTerminalId=bad", "originTerminalId=" + Guid.Empty,
      "destinationTerminalId=bad", "destinationTerminalId=" + Guid.Empty, "time=bad", "time=24:00", "time=07:30", "time=07:00:01", "time=07:00:00.001" })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(api + "?" + query)).StatusCode);
    var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    var hiddenChoices = (await client.GetFromJsonAsync<PartnerTripFilterOptions>(api + "/filter-options"))!;
    Assert.Empty(hiddenChoices.Times); Assert.Empty(hiddenChoices.Origins); Assert.Empty(hiddenChoices.Destinations);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>(api))!.Items);
  }
}
