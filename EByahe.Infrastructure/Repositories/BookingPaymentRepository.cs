using System.Data;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class BookingPaymentRepository(EByaheDbContext database) : IBookingPaymentRepository
{
  public async Task CompleteAsync(Guid incomingId, PaymentCompletedEvent payment, DateTime now, CancellationToken cancellationToken)
  {
    database.ChangeTracker.Clear();
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
    var receipt = await database.Incoming.FromSqlInterpolated(
      $"SELECT * FROM [Incoming] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {incomingId}").SingleAsync(cancellationToken);
    if (receipt.Status == "Processed") return;
    var booking = await database.BusBookings.FromSqlInterpolated(
      $"SELECT * FROM [BusBookings] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {payment.BookingId}").SingleOrDefaultAsync(cancellationToken)
      ?? throw new InvalidDataException("Payment references a missing booking.");
    using var snapshot = JsonDocument.Parse(booking.DetailsJson);
    if (booking.CreatedBy != payment.OwnerId || booking.Reference != payment.BookingReference
      || checked((long)(booking.Total * 100m)) != payment.AmountCentavos || payment.Currency != "PHP"
      || snapshot.RootElement.GetProperty("payment").GetProperty("method").GetString() == "cash"
      || payment.PaymentType is not (null or "GCash" or "Maya" or "Credit/Debit Card")
      || booking.PaymentType is not null && payment.PaymentType is not null && booking.PaymentType != payment.PaymentType)
      throw new InvalidDataException("Payment details do not match this booking.");
    if (booking.Status == "Cancelled" && payment.PaymentStatus != "Cancelled") throw new InvalidDataException("A cancelled booking cannot accept payment.");
    if (booking.PaymentId is not null && payment.PaymentStatus == "Payment Success"
      && (booking.PaymentId != payment.PaymentId || booking.ProviderPaymentId != payment.ProviderPaymentId))
      throw new InvalidDataException("A different payment already completed this booking.");
    if (payment.PaymentStatus is not ("Payment Success" or "Payment Failed" or "Cancelled")
      || payment.EventType != (payment.PaymentStatus == "Payment Success" ? "payment.completed" : payment.PaymentStatus == "Cancelled" ? "payment.cancelled" : "payment.failed"))
      throw new InvalidDataException("Unsupported payment outcome.");
    if (!await database.BusBookingStatusLogs.AnyAsync(x => x.EventId == payment.EventId, cancellationToken))
    {
      var previousStatus = booking.Status;
      var previousPaymentStatus = booking.PaymentStatus;
      var note = "Payment failure verified; no seats reserved.";
      if (payment.PaymentStatus == "Cancelled" && booking.PaymentStatus != "Payment Success")
      {
        booking.Status = "Cancelled";
        booking.PaymentStatus = "Cancelled";
        booking.PaymentType ??= payment.PaymentType;
        note = "Booking cancelled before payment because the requested seats or departure are no longer available. Any existing payable checkout was expired before cancellation.";
      }
      else if (payment.PaymentStatus == "Payment Success" && booking.PaymentStatus != "Payment Success")
      {
        booking.PaymentStatus = "Payment Success";
        booking.PaymentType ??= payment.PaymentType;
        booking.PaymentId = payment.PaymentId;
        booking.ProviderPaymentId = payment.ProviderPaymentId;
        booking.PaidAtUtc = payment.OccurredAtUtc;
        var passengers = snapshot.RootElement.GetProperty("booking").GetProperty("passengers").GetArrayLength();
        var trip = await database.ScheduledTrips.FromSqlInterpolated(
          $"SELECT * FROM [ScheduledTrips] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {booking.ScheduledTripId}")
          .Include(x => x.Bus).SingleOrDefaultAsync(cancellationToken);
        if (trip is not null && trip.Status == "Scheduled" && trip.DepartureAtUtc > new DateTimeOffset(now, TimeSpan.Zero)
          && trip.AvailableSeats is { } available && Math.Min(available, trip.Bus.SeatCapacity) >= passengers)
        {
          var previousTrip = CatalogAudit.Snapshot(trip);
          trip.AvailableSeats = Math.Min(available, trip.Bus.SeatCapacity) - passengers;
          CatalogAudit.Record(database, trip, booking.CreatedBy, "Updated", previousTrip);
          booking.ReservedSeats = passengers;
          booking.Status = "Confirmed";
          note = "Payment success verified; booking confirmed and seats reserved atomically.";
        }
        else
        {
          booking.PaymentReviewRequired = true;
          note = "Payment succeeded but seats or departure are unavailable. Manual refund or rebooking required; no seats reserved.";
        }
      }
      else if (booking.PaymentStatus != "Payment Success")
      {
        booking.PaymentStatus = "Payment Failed";
        booking.PaymentType ??= payment.PaymentType;
      }
      else note = "Duplicate success or late payment failure ignored; successful payment remains final.";
      database.BusBookingStatusLogs.Add(new BusBookingStatusLog
      {
        BookingId = booking.Id, PreviousStatus = previousStatus, Status = booking.Status,
        PreviousPaymentStatus = previousPaymentStatus, PaymentStatus = booking.PaymentStatus,
        PaymentType = booking.PaymentType, ChangedAtUtc = new DateTimeOffset(now, TimeSpan.Zero), EventId = payment.EventId, Remarks = note
      });
    }
    receipt.MessageId = payment.EventId;
    receipt.Status = "Processed";
    receipt.Error = null;
    receipt.RecordedDateTime = now;
    receipt.ProcessedDateTime = now;
    database.IncomingLogs.Add(new IncomingLog
    {
      Id = Guid.NewGuid(), IncomingId = receipt.Id, DeliveryId = receipt.DeliveryId,
      AttemptCount = receipt.AttemptCount, Status = "Processed", RecordedDateTime = now
    });
    // Confirming the booking and marking the inbox record processed are atomic.
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }
}
