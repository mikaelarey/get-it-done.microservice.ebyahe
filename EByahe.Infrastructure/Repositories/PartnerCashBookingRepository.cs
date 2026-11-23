using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed partial class PartnerPortalRepository
{
  public async Task<PartnerCashBookingSummary?> CashPaymentSummaryAsync(string userId, Guid bookingId, DateTimeOffset now, CancellationToken cancellationToken)
  {
    var booking = await BookingDetailsAsync(userId, bookingId, cancellationToken);
    if (booking is null) return null;
    var trip = await database.ScheduledTrips.AsNoTracking().Include(x => x.Bus).Include(x => x.BusRoute)
      .ThenInclude(x => x.OriginTerminal).Include(x => x.BusRoute).ThenInclude(x => x.DestinationTerminal)
      .SingleAsync(x => x.Id == booking.ScheduledTripId, cancellationToken);
    using var snapshot = JsonDocument.Parse(booking.DetailsJson);
    var method = snapshot.RootElement.GetProperty("payment").GetProperty("method").GetString() ?? "";
    var passengers = snapshot.RootElement.GetProperty("booking").GetProperty("passengers").GetArrayLength();
    var reason = method != "cash" ? "This booking uses an online payment method."
      : booking.PaymentStatus == "Payment Success" ? "Payment has already been received."
      : booking.Status != "Submitted" || booking.ReservedSeats != 0 ? "This booking cannot accept a cash payment."
      : now >= CashBookingPolicy.DueAt(booking) ? "The 24-hour cash payment deadline has passed. This booking will automatically cancel."
      : trip.Status != "Scheduled" || trip.DepartureAtUtc <= now ? "This departure is no longer available."
      : !trip.Bus.IsActive || !trip.BusRoute.IsActive || !trip.BusRoute.OriginTerminal.IsActive || !trip.BusRoute.DestinationTerminal.IsActive
        ? "This bus or route is no longer active."
      : trip.AvailableSeats is not { } available || Math.Min(available, trip.Bus.SeatCapacity) < passengers
        ? "There are not enough seats available for this booking." : null;
    return new(booking.Id, booking.Reference, booking.CreatedBy, trip.PartnerId, booking.Total, method, booking.Status,
      booking.PaymentStatus, passengers, reason is null, reason, method == "cash" ? CashBookingPolicy.DueAt(booking) : null);
  }
}
