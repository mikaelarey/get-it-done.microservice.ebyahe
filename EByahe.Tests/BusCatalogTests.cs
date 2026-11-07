using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Initialization;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private async Task SeedBookingTripAsync()
  {
    await using var db = CreateDatabase();
    var partner = new Partner { Code = "TEST", DisplayName = "Victory Liner" };
    var origin = new BusTerminal { Partner = partner, Location = new Location { Code = "MNL", Name = "Manila" }, Name = "Manila Bus Station" };
    var destination = new BusTerminal { Partner = partner, Location = new Location { Code = "BAG", Name = "Baguio" }, Name = "Baguio Bus Station" };
    var route = new BusRoute { Partner = partner, OriginTerminal = origin, DestinationTerminal = destination, DurationMinutes = 360 };
    var bus = new Bus { Partner = partner, FleetNumber = "TEST-1", PlateNumber = "TST-1234", BusClass = "Deluxe", SeatCapacity = 40 };
    var request = BookingRequest();
    db.ScheduledTrips.Add(new ScheduledTrip { Id = TestTripId, Partner = partner, Bus = bus, BusRoute = route, BaseFare = 750, AvailableSeats = 40,
      DepartureAtUtc = DateTimeOffset.Parse($"{request.Trip.DepartureDate}T06:00:00+08:00").ToUniversalTime() });
    await db.SaveChangesAsync();
  }

  [SqlServerFact]
  public async Task DemoCatalogSeedIsRepeatableAndSearchUsesDateDirectionAndCapacity()
  {
    await using var db = CreateDatabase();
    var seeder = new BusCatalogSeeder(db, TimeProvider.System);
    await seeder.SeedAsync();
    var count = await db.ScheduledTrips.CountAsync();
    var firstBus = await db.Buses.OrderBy(x => x.Id).FirstAsync();
    var originalPlate = firstBus.PlateNumber;
    Assert.NotNull(originalPlate);
    firstBus.PlateNumber = null; await db.SaveChangesAsync();
    await seeder.SeedAsync();
    Assert.Equal(originalPlate, firstBus.PlateNumber);
    firstBus.PlateNumber = "TST-9999"; await db.SaveChangesAsync();
    await seeder.SeedAsync(); Assert.Equal("TST-9999", firstBus.PlateNumber);
    Assert.Equal(66, await db.Buses.Select(x => x.PlateNumber).Distinct().CountAsync());
    var soldOutDemo = await db.ScheduledTrips.OrderBy(x => x.DepartureAtUtc).FirstAsync();
    soldOutDemo.AvailableSeats = 0; await db.SaveChangesAsync();
    await seeder.SeedAsync(); Assert.Equal(0, soldOutDemo.AvailableSeats);
    Assert.Equal(count, await db.ScheduledTrips.CountAsync());
    Assert.Equal(5, await db.Partners.CountAsync()); Assert.Equal(66, await db.Buses.CountAsync());
    Assert.Empty(await db.PartnerApplications.ToArrayAsync());
    Assert.All(await db.Partners.ToArrayAsync(), x => { Assert.True(x.IsDemo); Assert.Null(x.OwnerUserId); Assert.Null(x.PartnerApplicationId); });
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
    var locations = (await client.GetFromJsonAsync<BusLocationResponse[]>("/api/bus-catalog/locations"))!;
    var origin = locations.Single(x => x.Name == "Manila"); var destination = locations.Single(x => x.Name == "Baguio");
    var destinations = (await client.GetFromJsonAsync<BusLocationResponse[]>($"/api/bus-catalog/destinations?originId={origin.Id}"))!;
    Assert.Contains(destinations, x => x.Id == destination.Id); Assert.DoesNotContain(destinations, x => x.Name == "Cebu City");
    var date = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).AddDays(1).ToString("yyyy-MM-dd");
    var query = $"/api/bus-catalog/trips?originId={origin.Id}&destinationId={destination.Id}&departure={date}";
    var trips = (await client.GetFromJsonAsync<BusTripResponse[]>(query + "&passengers=2"))!;
    Assert.Equal(2, trips.Length); Assert.All(trips, x => { Assert.Equal(date, x.DepartureDate); Assert.Equal("Manila", x.Origin); Assert.Equal("Baguio", x.Destination); Assert.False(string.IsNullOrWhiteSpace(x.PlateNumber)); Assert.Equal(40, x.AvailableSeats); });
    var returned = await client.GetFromJsonAsync<BusTripResponse>($"/api/bus-catalog/trips/{trips[0].Id}");
    Assert.Equal(trips[0], returned);
    var repository = new BusCatalogRepository(db, TimeProvider.System);
    Assert.Empty(await repository.SearchAsync(origin.Id, destination.Id, DateOnly.Parse(date), 41, default));
    Assert.Empty(await repository.SearchAsync(origin.Id, destination.Id, DateOnly.Parse(date).AddDays(30), 1, default));
    var partner = await db.Partners.SingleAsync(x => x.DisplayName == "Victory Liner"); partner.Status = "Suspended";
    await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<BusTripResponse[]>(query))!);
    using var hidden = await client.GetAsync($"/api/bus-catalog/trips/{trips[0].Id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    foreach (var invalid in new[] { $"originId={Guid.Empty}&destinationId={destination.Id}&departure={date}", $"originId={origin.Id}&destinationId={destination.Id}&departure=bad", $"originId={origin.Id}&destinationId={destination.Id}", $"originId={origin.Id}&destinationId={destination.Id}&departure={date}&passengers=21" })
    {
      using var response = await client.GetAsync("/api/bus-catalog/trips?" + invalid); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
  }

  [SqlServerFact]
  public async Task TripAvailabilityComesFromTheScheduleAndLimitsSearchAndSubmission()
  {
    await SeedBookingTripAsync();
    await using var db = CreateDatabase();
    var schedule = await db.ScheduledTrips.SingleAsync(); schedule.AvailableSeats = 1; await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var trip = (await client.GetFromJsonAsync<BusTripResponse>($"/api/bus-catalog/trips/{schedule.Id}"))!;
    Assert.Equal(40, trip.Capacity); Assert.Equal(1, trip.AvailableSeats);
    var locations = (await client.GetFromJsonAsync<BusLocationResponse[]>("/api/bus-catalog/locations"))!;
    var origin = locations.Single(x => x.Name == "Manila");
    var destination = await db.Locations.SingleAsync(x => x.Name == "Baguio");
    var query = $"/api/bus-catalog/trips?originId={origin.Id}&destinationId={destination.Id}&departure={trip.DepartureDate}";
    Assert.Single((await client.GetFromJsonAsync<BusTripResponse[]>(query + "&passengers=1"))!);
    Assert.Empty((await client.GetFromJsonAsync<BusTripResponse[]>(query + "&passengers=2"))!);
    var request = BookingRequest();
    request.Booking.Passengers.Add(new() { FullName = "Second Traveler", Type = "adult" });
    request.Fare.Fares.Add(new() { Label = "12B Adult", Amount = 750m }); request.Fare.Fee = 45m; request.Fare.Total = 1545m;
    using var rejected = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    using var accepted = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest()); Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    await db.Entry(schedule).ReloadAsync(); Assert.Equal(0, schedule.AvailableSeats);
    schedule.AvailableSeats = 0; await db.SaveChangesAsync();
    using var soldOut = await client.GetAsync($"/api/bus-catalog/trips/{schedule.Id}"); Assert.Equal(HttpStatusCode.NotFound, soldOut.StatusCode);
    Assert.Empty((await client.GetFromJsonAsync<BusTripResponse[]>(query))!);
    schedule.AvailableSeats = null; await db.SaveChangesAsync();
    using var unknown = await client.GetAsync($"/api/bus-catalog/trips/{schedule.Id}"); Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
  }

  [SqlServerFact]
  public async Task BookingRejectsChangedCatalogDetailsAndFaresButReplaysAcceptedRequests()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    foreach (var change in new Action<EByahe.Application.Dtos.Requests.CreateBusBookingRequest>[] {
      x => x.Trip.TripId = MissingTripId, x => x.Trip.DepartureDate = DateTime.UtcNow.AddDays(8).ToString("yyyy-MM-dd"),
      x => x.Trip.Operator = "Another partner", x => x.Booking.Boarding = "Another terminal",
      x => x.Trip.PlateNumber = "FAKE-0000",
      x => { x.Fare.Fares[0].Amount = 1; x.Fare.Fee = .03m; x.Fare.Total = 1.03m; }
    })
    {
      var invalid = BookingRequest(); change(invalid);
      using var response = await client.PostAsJsonAsync("/api/bus-bookings", invalid); Assert.Equal(invalid.Trip.TripId == MissingTripId ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, response.StatusCode);
    }
    var request = BookingRequest();
    using var created = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var receipt = (await created.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    var details = (await client.GetFromJsonAsync<BusBookingDetailsResponse>($"/api/bus-bookings/{receipt.Id}"))!;
    Assert.Equal("TST-1234", details.Trip.PlateNumber);
    await using var db = CreateDatabase();
    Assert.Equal(TestTripId, (await db.BusBookings.SingleAsync()).ScheduledTripId);
    var bus = await db.Buses.SingleAsync(); bus.PlateNumber = "TST-5678"; await db.SaveChangesAsync();
    details = (await client.GetFromJsonAsync<BusBookingDetailsResponse>($"/api/bus-bookings/{receipt.Id}"))!;
    Assert.Equal("TST-1234", details.Trip.PlateNumber);
    var schedule = await db.ScheduledTrips.SingleAsync(); schedule.Status = "Cancelled"; await db.SaveChangesAsync();
    using var replay = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    request.RequestId = Guid.NewGuid();
    using var cancelled = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
  }
}
