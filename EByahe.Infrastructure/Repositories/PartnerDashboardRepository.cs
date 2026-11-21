using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed partial class PartnerPortalRepository
{
  public async Task<PartnerDashboardOverview> DashboardOverviewAsync(string userId, string period, DateTimeOffset now,
    PartnerDashboardAccess access, CancellationToken cancellationToken)
  {
    if (period is not ("week" or "month")) throw new ArgumentOutOfRangeException(nameof(period));
    var days = period == "month" ? 30 : 7;
    var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(8)).DateTime);
    var first = today.AddDays(1 - days);
    static DateTimeOffset Midnight(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8));
    var start = Midnight(first).ToUniversalTime(); var end = Midnight(today.AddDays(1)).ToUniversalTime();
    var ids = PartnerIds(userId);
    var bookings = Bookings(userId).Where(row => row.CreatedAtUtc <= now);
    var trips = database.ScheduledTrips.AsNoTracking().Where(row => ids.Contains(row.PartnerId));
    var confirmed = CodeDecodeValues.Code("BookingStatuses", "Confirmed");
    var submitted = CodeDecodeValues.Code("BookingStatuses", "Submitted");
    var cancelled = CodeDecodeValues.Code("BookingStatuses", "Cancelled");
    var paid = CodeDecodeValues.Code("PaymentStatuses", "Payment Success");
    var scheduled = CodeDecodeValues.Code("ScheduledTripStatuses", "Scheduled");

    async Task<PartnerDashboardMetrics> Metrics(DateTimeOffset from, DateTimeOffset to)
    {
      var totals = access.Bookings ? await bookings.Where(row => row.CreatedAtUtc >= from && row.CreatedAtUtc < to)
        .GroupBy(_ => 1).Select(group => new {
          Count = group.Count(), Value = group.Sum(row => row.CodeDecodeBookingStatus != cancelled && row.CodeDecodePaymentStatus == paid ? row.Total : 0),
          Confirmed = group.Count(row => row.CodeDecodeBookingStatus == confirmed),
          Submitted = group.Count(row => row.CodeDecodeBookingStatus == submitted),
          Cancelled = group.Count(row => row.CodeDecodeBookingStatus == cancelled)
        }).SingleOrDefaultAsync(cancellationToken) : null;
      var periodTrips = trips.Where(row => row.DepartureAtUtc >= from && row.DepartureAtUtc < to && row.CodeDecodeScheduledTripStatus == scheduled);
      var tripCount = access.Trips ? await periodTrips.CountAsync(cancellationToken) : 0;
      decimal occupancy = 0;
      if (access.Trips)
      {
        var inventory = periodTrips.Where(row => row.AvailableSeats != null && row.Bus.SeatCapacity > 0);
        var capacity = await inventory.SumAsync(row => (long?)row.Bus.SeatCapacity, cancellationToken) ?? 0;
        // Unpaid / submitted bookings never count as reserved seats. Keep this
        // consistent with booking confirmation, rather than counting passengers.
        var reserved = await bookings.Where(row => row.CodeDecodeBookingStatus == confirmed && row.CodeDecodePaymentStatus == paid
          && inventory.Any(trip => trip.Id == row.ScheduledTripId)).SumAsync(row => (long?)row.ReservedSeats, cancellationToken) ?? 0;
        if (capacity > 0) occupancy = Math.Round(Math.Clamp(100m * reserved / capacity, 0, 100), 1);
      }
      return new(totals?.Count ?? 0, totals?.Value ?? 0, tripCount, occupancy, totals?.Confirmed ?? 0, totals?.Submitted ?? 0, totals?.Cancelled ?? 0);
    }

    var current = await Metrics(start, end); var previous = await Metrics(start.AddDays(-days), start);
    var daily = access.Bookings ? await bookings.Where(row => row.CreatedAtUtc >= start && row.CreatedAtUtc < end)
      .GroupBy(row => row.CreatedAtUtc.AddHours(8).Date)
      .Select(group => new { Date = group.Key, Count = group.Count() }).ToArrayAsync(cancellationToken) : [];
    var counts = daily.ToDictionary(row => DateOnly.FromDateTime(row.Date), row => row.Count);
    var chart = Enumerable.Range(0, days).Select(index => first.AddDays(index))
      .Select(date => new PartnerDashboardDay(date, counts.GetValueOrDefault(date))).ToArray();
    var terminalCount = access.Terminals ? await database.BusTerminals.CountAsync(row => ids.Contains(row.PartnerId), cancellationToken) : 0;
    var userCount = access.Users ? await database.PartnerUsers.Where(row => row.IsActive && ids.Contains(row.PartnerId))
      .Select(row => row.RefUserId).Distinct().CountAsync(cancellationToken) : 0;

    var departures = access.Trips ? await trips.Where(row => row.CodeDecodeScheduledTripStatus == scheduled && row.DepartureAtUtc >= now
        && row.Bus.IsActive && row.BusRoute.IsActive && row.BusRoute.OriginTerminal.IsActive && row.BusRoute.DestinationTerminal.IsActive)
      .OrderBy(row => row.DepartureAtUtc).ThenBy(row => row.Id).Take(3)
      .Select(row => new PartnerDashboardDeparture(row.Id, row.BusRoute.OriginTerminal.Location.Name, row.BusRoute.DestinationTerminal.Location.Name,
        row.BusRoute.OriginTerminal.Name, row.Bus.BusClass.Decode, row.Bus.PlateNumber, row.DepartureAtUtc, row.Bus.SeatCapacity,
        bookings.Where(booking => booking.ScheduledTripId == row.Id && booking.CodeDecodeBookingStatus == confirmed && booking.CodeDecodePaymentStatus == paid)
          .Sum(booking => (int?)booking.ReservedSeats) ?? 0, row.AvailableSeats)).ToArrayAsync(cancellationToken) : [];
    // Only the five latest snapshots are read. No full history or private JSON
    // documents leave the repository in the dashboard response.
    var latest = access.Bookings ? await bookings.OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id).Take(5).ToArrayAsync(cancellationToken) : [];
    var recent = latest.Select(DashboardBooking).ToArray();
    var activities = new List<PartnerDashboardActivity>();
    if (access.Bookings)
    {
      var changes = await (from log in database.BusBookingStatusLogs.AsNoTracking()
        join booking in bookings on log.BookingId equals booking.Id
        where log.ChangedAtUtc <= now
        orderby log.ChangedAtUtc descending, log.Id descending
        select new { log.Id, booking.Reference, log.CodeDecodeBookingStatus, log.CodeDecodePaymentStatus, log.ChangedAtUtc }).Take(5).ToArrayAsync(cancellationToken);
      activities.AddRange(changes.Select(row => new PartnerDashboardActivity("booking-log-" + row.Id,
        "Booking " + CodeDecodeValues.Decode("BookingStatuses", row.CodeDecodeBookingStatus).ToLowerInvariant(),
        row.Reference + " · " + CodeDecodeValues.Decode("PaymentStatuses", row.CodeDecodePaymentStatus), row.ChangedAtUtc, "bookings")));
      var latestIds = latest.Select(row => row.Id).ToArray();
      var loggedIds = await database.BusBookingStatusLogs.Where(log => latestIds.Contains(log.BookingId))
        .Select(log => log.BookingId).Distinct().ToArrayAsync(cancellationToken);
      activities.AddRange(latest.Where(row => !loggedIds.Contains(row.Id))
        .Select(row => new PartnerDashboardActivity("booking-" + row.Id, "New booking received", row.Reference, row.CreatedAtUtc, "bookings")));
    }
    if (access.Trips)
      activities.AddRange(await (from log in database.ScheduledTripLogs.AsNoTracking() join trip in trips on log.ScheduledTripId equals trip.Id
        where log.DateTimeCreated <= now orderby log.DateTimeCreated descending, log.Id descending
        select new PartnerDashboardActivity("trip-" + log.Id, log.Action + " scheduled trip",
          trip.BusRoute.OriginTerminal.Location.Name + " → " + trip.BusRoute.DestinationTerminal.Location.Name, log.DateTimeCreated, "scheduled-trips"))
        .Take(5).ToArrayAsync(cancellationToken));
    if (access.Terminals)
      activities.AddRange(await (from log in database.TerminalLogs.AsNoTracking() join terminal in database.BusTerminals on log.TerminalId equals terminal.Id
        where ids.Contains(terminal.PartnerId) && log.DateTimeCreated <= now orderby log.DateTimeCreated descending, log.Id descending
        select new PartnerDashboardActivity("terminal-" + log.Id, log.Action + " terminal", terminal.Name, log.DateTimeCreated, "terminals"))
        .Take(5).ToArrayAsync(cancellationToken));
    if (access.Buses)
      activities.AddRange(await (from log in database.BusLogs.AsNoTracking() join bus in database.Buses on log.BusId equals bus.Id
        where ids.Contains(bus.PartnerId) && log.DateTimeCreated <= now orderby log.DateTimeCreated descending, log.Id descending
        select new PartnerDashboardActivity("bus-" + log.Id, log.Action + " bus", bus.FleetNumber, log.DateTimeCreated, "buses"))
        .Take(5).ToArrayAsync(cancellationToken));
    if (access.Users)
      activities.AddRange(await database.PartnerUsers.AsNoTracking().Where(row => ids.Contains(row.PartnerId) && row.CreatedAtUtc <= now)
        .OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id).Take(5)
        .Select(row => new PartnerDashboardActivity("member-" + row.Id, "Team member added",
          (row.RefUser.FirstName + " " + row.RefUser.LastName).Trim(), row.CreatedAtUtc, "users")).ToArrayAsync(cancellationToken));
    return new(period, first, today, now, access, current, previous, terminalCount, userCount, chart, departures, recent,
      activities.OrderByDescending(row => row.AtUtc).ThenByDescending(row => row.Id).Take(5).ToArray());
  }

  private static PartnerDashboardBooking DashboardBooking(BusBooking booking)
  {
    using var snapshot = JsonDocument.Parse(booking.DetailsJson);
    var root = snapshot.RootElement;
    var hasTrip = root.TryGetProperty("trip", out var trip) && trip.ValueKind == JsonValueKind.Object;
    static string? Text(JsonElement element, string key) => element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    string? TripText(string key) => hasTrip ? Text(trip, key) : null;
    var count = booking.ReservedSeats; string? passenger = null;
    if (root.TryGetProperty("booking", out var details) && details.ValueKind == JsonValueKind.Object
      && details.TryGetProperty("passengers", out var people) && people.ValueKind == JsonValueKind.Array)
    {
      count = people.GetArrayLength();
      if (count > 0 && people[0].ValueKind == JsonValueKind.Object) passenger = Text(people[0], "fullName");
    }
    return new(booking.Id, booking.Reference, passenger, TripText("origin"), TripText("destination"), TripText("departureDate"),
      TripText("departureTime"), count, booking.Total, booking.Status);
  }
}
