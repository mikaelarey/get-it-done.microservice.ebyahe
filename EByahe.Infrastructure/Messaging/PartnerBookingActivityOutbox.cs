using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Messaging;

internal static class PartnerBookingActivityOutbox
{
  public static async Task<IReadOnlyList<OutboxMessage>> QueueAsync(EByaheDbContext db, BusBooking booking,
    Guid partnerId, string topic, CancellationToken ct)
  {
    var members = await db.PartnerUsers.AsNoTracking().Where(member => member.PartnerId == partnerId
      && member.IsActive && member.Partner.Status == "Approved")
      .Select(member => member.RefUser.ExternalUserId).Distinct().OrderBy(subject => subject).ToArrayAsync(ct);
    var subjects = members.Where(subject => !string.Equals(subject, booking.CreatedBy, StringComparison.Ordinal)).ToArray();
    // An empty team still records the activity, without falling back to administrators.
    var batches = subjects.Length == 0 ? new[] { Array.Empty<string>() } : subjects.Chunk(25).ToArray();
    var events = new List<OutboxMessage>();
    foreach (var batch in batches)
    {
      var message = ActivityOutbox.Create(new ActivityEvent(events.Count == 0 ? booking.Id : Guid.NewGuid(),
        "booking.created", 1, booking.CreatedAtUtc, "ebyahe", booking.CreatedBy, "Created a bus booking.",
        "/bookings/" + booking.Id.ToString("D"),
        Details: new { booking.Id, booking.Reference, booking.ScheduledTripId, partnerId, booking.Total, booking.ReservedSeats, booking.Status },
        Audiences: batch.Select(subject => new { kind = "User", externalUserId = subject }).ToArray()), topic);
      db.OutboxMessages.Add(message);
      events.Add(message);
    }
    return events;
  }

  public static void Detach(EByaheDbContext db, IEnumerable<OutboxMessage> events)
  {
    foreach (var message in events)
    {
      foreach (var history in message.History.ToArray()) db.Entry(history).State = EntityState.Detached;
      db.Entry(message).State = EntityState.Detached;
    }
  }
}
