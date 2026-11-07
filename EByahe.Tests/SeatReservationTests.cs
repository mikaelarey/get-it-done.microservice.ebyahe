using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static CreateBusBookingRequest ReservationRequest(int passengers)
  {
    var request = BookingRequest();
    request.Booking.Passengers = Enumerable.Range(1, passengers)
      .Select(index => new BusPassengerRequest { FullName = $"Traveler {index}", Type = "adult" }).ToList();
    request.Fare.Fares = Enumerable.Range(1, passengers)
      .Select(index => new BusFareLineRequest { Label = $"Passenger {index}", Amount = 750m }).ToList();
    request.Fare.Fee = 750m * passengers * .03m; request.Fare.Total = 750m * passengers + request.Fare.Fee;
    return request;
  }

  [SqlServerFact]
  public async Task SeatReservationsAreScopedToTheSelectedDepartureAndReplayAfterSellout()
  {
    await SeedBookingTripAsync();
    await using var db = CreateDatabase();
    var first = await db.ScheduledTrips.SingleAsync(); first.AvailableSeats = 2;
    var second = new ScheduledTrip { PartnerId = first.PartnerId, BusId = first.BusId, BusRouteId = first.BusRouteId,
      DepartureAtUtc = first.DepartureAtUtc.AddDays(1), BaseFare = first.BaseFare, AvailableSeats = 4 };
    db.ScheduledTrips.Add(second); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var request = ReservationRequest(2);
    using var response = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var receipt = (await response.Content.ReadFromJsonAsync<BusBookingResponse>())!; Assert.Equal(2, receipt.ReservedSeats);
    var details = (await client.GetFromJsonAsync<BusBookingDetailsResponse>($"/api/bus-bookings/{receipt.Id}"))!;
    Assert.Equal(2, details.ReservedSeats); Assert.Equal("Passenger 1 · Adult", details.Fare.Fares[0].Label);
    await db.Entry(first).ReloadAsync(); await db.Entry(second).ReloadAsync();
    Assert.Equal(0, first.AvailableSeats); Assert.Equal(4, second.AvailableSeats);
    using var replay = await client.PostAsJsonAsync("/api/bus-bookings", request); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
    Assert.Equal(receipt, await replay.Content.ReadFromJsonAsync<BusBookingResponse>());
    var next = ReservationRequest(1); next.Trip.TripId = second.Id;
    next.Trip.DepartureDate = second.DepartureAtUtc.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd");
    using var nextResponse = await client.PostAsJsonAsync("/api/bus-bookings", next); Assert.Equal(HttpStatusCode.Created, nextResponse.StatusCode);
    await db.Entry(first).ReloadAsync(); await db.Entry(second).ReloadAsync();
    Assert.Equal(0, first.AvailableSeats); Assert.Equal(3, second.AvailableSeats);
    Assert.Equal(3, await db.BusBookings.SumAsync(x => x.ReservedSeats));
    var page = (await client.GetFromJsonAsync<BusBookingPage>("/api/bus-bookings"))!;
    Assert.All(page.Items, row => Assert.Equal(row.Passengers, row.ReservedSeats));
  }

  [SqlServerFact]
  public async Task ConcurrentSubmissionsCannotOverbookTheLastSeats()
  {
    await SeedBookingTripAsync();
    await using var db = CreateDatabase();
    var schedule = await db.ScheduledTrips.SingleAsync(); schedule.AvailableSeats = 3; await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var requests = Enumerable.Range(0, 12).Select(_ => ReservationRequest(1)).ToArray();
    var responses = await Task.WhenAll(requests.Select(request => client.PostAsJsonAsync("/api/bus-bookings", request)));
    try
    {
      Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
      Assert.Equal(9, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
      var acceptedIndex = Array.FindIndex(responses, x => x.StatusCode == HttpStatusCode.Created);
      var receipt = (await responses[acceptedIndex].Content.ReadFromJsonAsync<BusBookingResponse>())!;
      using var retry = await client.PostAsJsonAsync("/api/bus-bookings", requests[acceptedIndex]);
      Assert.Equal(HttpStatusCode.OK, retry.StatusCode); Assert.Equal(receipt, await retry.Content.ReadFromJsonAsync<BusBookingResponse>());
      await db.Entry(schedule).ReloadAsync(); Assert.Equal(0, schedule.AvailableSeats);
      Assert.Equal(3, await db.BusBookings.CountAsync()); Assert.Equal(3, await db.BusBookings.SumAsync(x => x.ReservedSeats));
    }
    finally { foreach (var response in responses) response.Dispose(); }
  }

  [SqlServerFact]
  public async Task AFailedBookingInsertRollsBackTheSeatDecrement()
  {
    await SeedBookingTripAsync();
    await using var db = CreateDatabase();
    var existing = new BusBooking { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), CreatedBy = "test-user",
      Reference = "BK-INSERT-FAILURE", CreatedAtUtc = DateTimeOffset.UtcNow, RequestHash = new string('A', 64), DetailsJson = "{}" };
    db.BusBookings.Add(existing); await db.SaveChangesAsync();
    var trip = (await new BusCatalogRepository(db, TimeProvider.System).GetAsync(TestTripId, default))!;
    var conflicting = new BusBooking { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), CreatedBy = "test-user",
      ScheduledTripId = trip.Id, Reference = existing.Reference, CreatedAtUtc = DateTimeOffset.UtcNow,
      RequestHash = new string('B', 64), DetailsJson = "{}" };
    await Assert.ThrowsAsync<DbUpdateException>(() => new BusBookingRepository(db, TimeProvider.System)
      .ReserveAndAddAsync(conflicting, trip, 2, default));
    await using var verify = CreateDatabase();
    Assert.Equal(40, (await verify.ScheduledTrips.SingleAsync()).AvailableSeats);
    Assert.Equal(0, (await verify.BusBookings.SingleAsync()).ReservedSeats);
  }
}
