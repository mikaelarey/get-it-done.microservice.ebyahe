using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ScheduledTripFiltersUseManilaDayEitherTerminalAndScopedDatabasePaginationForReaders()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime);
    var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
    var route = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    ScheduledTrip Trip(BusRoute path, DateTimeOffset departure) => new() { PartnerId = partner.Id, BusId = bus.Id, BusRoute = path, DepartureAtUtc = departure, AvailableSeats = 40, BaseFare = 500 };
    for (var i = 0; i < 12; i++) db.ScheduledTrips.Add(Trip(route, start.AddMinutes(i)));
    db.ScheduledTrips.AddRange(Trip(route, start.AddTicks(-10)), Trip(route, start.AddDays(1)));
    var other = new BusTerminal { PartnerId = partner.Id, Name = "Other terminal", LocationId = origin.LocationId, IsActive = false };
    var otherRoute = new BusRoute { PartnerId = partner.Id, OriginTerminal = other, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    db.ScheduledTrips.Add(Trip(otherRoute, start.AddHours(10)));
    var foreign = new Partner { Code = "FILTER-FOREIGN", DisplayName = "Foreign company", Status = "Approved" };
    var foreignTerminal = new BusTerminal { Partner = foreign, Name = "Hidden terminal", LocationId = origin.LocationId };
    db.BusTerminals.Add(foreignTerminal); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string api = "/api/partner/scheduled-trips";
    foreach (var roles in new object?[] { null, BusRoles("bus.read"), BusRoles("scheduled-trip.write") })
    {
      client.DefaultRequestHeaders.Authorization = roles is null ? null : new("Bearer", factory.Token(resourceAccess: roles));
      var status = roles is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
      Assert.Equal(status, (await client.GetAsync(api)).StatusCode); Assert.Equal(status, (await client.GetAsync(api + "/filter-terminals")).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read")));
    var terminals = (await client.GetFromJsonAsync<PartnerTripFilterTerminal[]>(api + "/filter-terminals"))!;
    Assert.Equal(3, terminals.Length); Assert.Contains(terminals, terminal => terminal.Id == other.Id); Assert.DoesNotContain(terminals, terminal => terminal.Id == foreignTerminal.Id);
    Assert.All(terminals, terminal => Assert.Equal(partner.DisplayName, terminal.Partner));
    var today = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>(api + "?pageSize=5"))!;
    Assert.Equal(13, today.TotalCount); Assert.Equal(5, today.Items.Count); Assert.Equal(start, today.Items[0].DepartureAtUtc);
    Assert.All(today.Items, trip => Assert.InRange(trip.DepartureAtUtc, start, start.AddDays(1).AddTicks(-1)));
    var final = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date:yyyy-MM-dd}&terminalId={origin.Id}&pageSize=5&page=3"))!;
    Assert.Equal(12, final.TotalCount); Assert.Equal(2, final.Items.Count);
    var byDestination = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date:yyyy-MM-dd}&terminalId={destination.Id}"))!;
    Assert.Equal(13, byDestination.TotalCount);
    var onlyOther = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date:yyyy-MM-dd}&terminalId={other.Id}"))!;
    Assert.Equal(1, onlyOther.TotalCount);
    var tomorrow = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date.AddDays(1):yyyy-MM-dd}"))!; Assert.Single(tomorrow.Items);
    var yesterday = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?date={date.AddDays(-1):yyyy-MM-dd}"))!; Assert.Single(yesterday.Items);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>($"{api}?terminalId={foreignTerminal.Id}"))!.Items);
    foreach (var query in new[] { "date=bad", "date=2026-02-30", "date=0001-01-01", "terminalId=bad", "terminalId=" + Guid.Empty })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(api + "?" + query)).StatusCode);
    var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerTripFilterTerminal[]>(api + "/filter-terminals"))!);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>(api))!.Items);
  }
}
