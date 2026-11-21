using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PartnerDashboardUsesManilaPeriodsPaidSeatReservationsScopedRecentRecordsAndReadPermissions()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var now = new DateTimeOffset(2026, 11, 19, 4, 0, 0, TimeSpan.Zero);
    var first = new DateTimeOffset(2026, 11, 12, 16, 0, 0, TimeSpan.Zero); // Nov 13, midnight Manila.
    var route = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    ScheduledTrip Trip(DateTimeOffset at, int? available = 40, string status = "Scheduled") => new() {
      PartnerId = partner.Id, BusId = bus.Id, BusRoute = route, DepartureAtUtc = at, AvailableSeats = available, BaseFare = 500, Status = status };
    var past = Trip(now.AddDays(-1)); var next = Trip(now.AddHours(2), 38); var previous = Trip(first.AddDays(-2), 36);
    var unknown = Trip(now.AddDays(1), null); var cancelledTrip = Trip(now.AddHours(1), status: "Cancelled");
    var foreign = new Partner { Code = "DASH-FOREIGN", DisplayName = "Private operator" };
    var hidden = new ScheduledTrip { Partner = foreign, Bus = new Bus { Partner = foreign, FleetNumber = "PRIVATE", CodeDecodeBusClass = 2 },
      BusRoute = new BusRoute { Partner = foreign, OriginTerminal = new BusTerminal { Partner = foreign, Name = "Private origin", LocationId = origin.LocationId },
        DestinationTerminal = new BusTerminal { Partner = foreign, Name = "Private destination", LocationId = destination.LocationId }, DurationMinutes = 60 },
      DepartureAtUtc = now.AddMinutes(1), AvailableSeats = 40, BaseFare = 99999 };
    db.ScheduledTrips.AddRange(past, next, previous, unknown, cancelledTrip, hidden);
    BusBooking Booking(string reference, ScheduledTrip trip, DateTimeOffset created, decimal total, string status = "Submitted", string payment = "Pending Payment", int reserved = 0) => new() {
      Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), Reference = reference, ScheduledTripId = trip.Id, CreatedBy = PartnerApiFactory.Subject,
      CreatedAtUtc = created, RequestHash = new string('A', 64), Total = total, Status = status, PaymentStatus = payment, ReservedSeats = reserved,
      DetailsJson = JsonSerializer.Serialize(new { trip = new { origin = "Origin city", destination = "Destination city", departureDate = "2026-11-19", departureTime = "14:00" },
        booking = new { passengers = new[] { new { fullName = "Actual Passenger" }, new { fullName = "Second Passenger" } }, contact = new { email = "private-contact@example.com" } } }) };
    var paid = Booking("BK-PAID", next, first, 1000, "Confirmed", "Payment Success", 2);
    paid.PaymentType = "GCash";
    var submitted = Booking("BK-SUBMITTED", next, now.AddHours(-1), 1500);
    var failed = Booking("BK-FAILED", next, now.AddMinutes(-1), 500, payment: "Payment Failed");
    var cancelled = Booking("BK-CANCELLED", next, now.AddDays(-2), 2000, "Cancelled", "Cancelled");
    db.BusBookings.AddRange(paid, submitted, failed, cancelled,
      Booking("BK-PREVIOUS", previous, first.AddDays(-2), 800, "Confirmed", "Payment Success", 4),
      Booking("BK-BOUNDARY", previous, first.AddTicks(-1), 200),
      Booking("PRIVATE-BOOKING", hidden, now.AddSeconds(-1), 99999, payment: "Payment Success"),
      Booking("BK-FUTURE", next, now.AddDays(1), 55555, payment: "Payment Success"));
    db.BusBookingStatusLogs.Add(new BusBookingStatusLog { BookingId = failed.Id, Status = "Submitted", PaymentStatus = "Payment Failed", ChangedAtUtc = now.AddMinutes(-1), Remarks = "Private remarks" });
    db.ScheduledTripLogs.Add(new ScheduledTripLog { ScheduledTripId = next.Id, CreatedBy = PartnerApiFactory.Subject, DateTimeCreated = now.AddMinutes(-2), Action = "Updated", CurrentValue = "{}" });
    db.BusLogs.Add(new BusLog { BusId = bus.Id, CreatedBy = PartnerApiFactory.Subject, DateTimeCreated = now.AddMinutes(-3), Action = "Created", CurrentValue = "{}" });
    db.TerminalLogs.Add(new TerminalLog { TerminalId = origin.Id, CreatedBy = PartnerApiFactory.Subject, DateTimeCreated = now.AddMinutes(-4), Action = "Created", CurrentValue = "{}" });
    await db.SaveChangesAsync();
    var repository = new PartnerPortalRepository(db); var access = new PartnerDashboardAccess(true, true, true, true, true);
    var data = await repository.DashboardOverviewAsync(PartnerApiFactory.Subject, "week", now, access, default);
    Assert.Equal(new DateOnly(2026, 11, 13), data.StartDate); Assert.Equal(new DateOnly(2026, 11, 19), data.EndDate);
    Assert.Equal(new PartnerDashboardMetrics(4, 1000, 2, 2.5m, 1, 2, 1), data.Current);
    Assert.Equal(new PartnerDashboardMetrics(2, 800, 1, 10, 1, 1, 0), data.Previous);
    Assert.Equal(7, data.Chart.Count); Assert.Equal(4, data.Chart.Sum(row => row.Bookings)); Assert.Equal(1, data.Chart[0].Bookings);
    Assert.Equal(2, data.TerminalCount); Assert.Equal(1, data.ActiveUserCount);
    Assert.Equal(new[] { next.Id, unknown.Id }, data.Departures.Select(row => row.Id)); Assert.Equal(2, data.Departures[0].ReservedSeats); Assert.Equal(38, data.Departures[0].AvailableSeats); Assert.Null(data.Departures[1].AvailableSeats);
    Assert.Equal(5, data.RecentBookings.Count); Assert.Equal("BK-FAILED", data.RecentBookings[0].Reference); Assert.Equal("Actual Passenger", data.RecentBookings[0].Passenger);
    Assert.Equal(5, data.Activity.Count); Assert.Contains(data.Activity, row => row.Resource == "scheduled-trips");
    Assert.Contains(data.Activity, row => row.Detail == "BK-FAILED · Payment Failed");
    var serialized = JsonSerializer.Serialize(data);
    foreach (var text in new[] { "PRIVATE", "private-contact", "Private remarks", "DetailsJson", "BK-FUTURE" }) Assert.DoesNotContain(text, serialized);
    var month = await repository.DashboardOverviewAsync(PartnerApiFactory.Subject, "month", now, access, default);
    Assert.Equal(30, month.Chart.Count); Assert.Equal(6, month.Current.Bookings);
    Assert.Equal(1800, month.Current.BookingValue);
    // Accepted cash payments count alongside online payments; pending and failed payments do not.
    submitted.PaymentType = "Cash"; submitted.PaymentStatus = "Payment Success";
    submitted.Status = "Confirmed"; submitted.ReservedSeats = 2;
    await db.SaveChangesAsync();
    var afterCashAcceptance = await repository.DashboardOverviewAsync(PartnerApiFactory.Subject, "week", now, access, default);
    Assert.Equal(2500, afterCashAcceptance.Current.BookingValue);
    Assert.Equal(4, afterCashAcceptance.Current.Bookings);
    Assert.Equal(800, afterCashAcceptance.Previous.BookingValue);
    paid.Status = "Submitted"; paid.ReservedSeats = 0; paid.PaymentStatus = "Payment Failed";
    submitted.Status = "Submitted"; submitted.ReservedSeats = 0; submitted.PaymentStatus = "Pending Payment";
    await db.SaveChangesAsync();
    var noSuccessfulPayments = await repository.DashboardOverviewAsync(PartnerApiFactory.Subject, "week", now, access, default);
    Assert.Equal(0, noSuccessfulPayments.Current.BookingValue);
    Assert.Equal(4, noSuccessfulPayments.Current.Bookings);
    var restricted = await repository.DashboardOverviewAsync(PartnerApiFactory.Subject, "week", now, new(false, false, false, true, false), default);
    Assert.Equal(new PartnerDashboardMetrics(0, 0, 0, 0, 0, 0, 0), restricted.Current); Assert.Empty(restricted.Departures); Assert.Empty(restricted.RecentBookings);
    Assert.All(restricted.Activity, row => Assert.Equal("users", row.Resource)); Assert.Equal(0, restricted.TerminalCount);
    var member = await db.PartnerUsers.SingleAsync(row => row.RefUser.ExternalUserId == PartnerApiFactory.Subject); member.IsActive = false; await db.SaveChangesAsync();
    var revoked = await repository.DashboardOverviewAsync(PartnerApiFactory.Subject, "week", now, access, default);
    Assert.Equal(new PartnerDashboardMetrics(0, 0, 0, 0, 0, 0, 0), revoked.Current); Assert.Empty(revoked.Departures); Assert.Empty(revoked.RecentBookings); Assert.Empty(revoked.Activity);
  }

  [SqlServerFact]
  public async Task PartnerDashboardEndpointValidatesPeriodsRequiresPartnerAndLimitsSectionsToExactRoles()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string path = "/api/partner/dashboard/overview";
    client.DefaultRequestHeaders.Authorization = null; Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("booking.read")));
    Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "?period=year")).StatusCode);
    using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var data = (await response.Content.ReadFromJsonAsync<PartnerDashboardOverview>())!;
    Assert.Equal(new PartnerDashboardAccess(true, false, false, false, false), data.Access);
    Assert.Equal(7, data.Chart.Count); Assert.Equal(0, data.Current.Bookings); Assert.Empty(data.Activity);
    Assert.Equal(30, (await client.GetFromJsonAsync<PartnerDashboardOverview>(path + "?period=month"))!.Chart.Count);
  }
}
