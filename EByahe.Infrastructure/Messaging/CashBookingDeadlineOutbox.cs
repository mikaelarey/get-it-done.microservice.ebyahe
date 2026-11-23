using System.Text.Json;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;

namespace EByahe.Infrastructure.Messaging;

public static class CashBookingDeadlineOutbox
{
  public const string DefaultTopic = "get-it-done.ebyahe.cash-bookings.v1";
  public static OutboxMessage Queue(EByaheDbContext db, BusBooking booking, Guid partnerId, string topic)
  {
    var id = Guid.NewGuid();
    var message = new OutboxMessage {
      Id = id, UserId = booking.CreatedBy, EventType = "booking.created", Topic = topic, MessageKey = booking.Id.ToString(),
      Payload = JsonSerializer.Serialize(new { eventId = id, eventType = "booking.created", schemaVersion = 1,
        bookingId = booking.Id, bookingReference = booking.Reference, ownerId = booking.CreatedBy, partnerId,
        amountCentavos = checked((long)(booking.Total * 100m)), currency = "PHP", paymentMethod = "cash",
        createdAtUtc = booking.CreatedAtUtc, dueAtUtc = CashBookingPolicy.DueAt(booking) }),
      CreatedDateTime = booking.CreatedAtUtc.UtcDateTime, NextAttemptDateTime = booking.CreatedAtUtc.UtcDateTime
    };
    message.History.Add(Outgoing.Record(message, "Pending", booking.CreatedAtUtc.UtcDateTime));
    db.OutboxMessages.Add(message);
    return message;
  }
}
