using EByahe.Domain.Models;
using EByahe.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
namespace EByahe.Infrastructure.Persistence;
public sealed partial class EByaheDbContext
{
  private List<OutboxMessage> PrepareActivityEvents()
  {
    var activities = new List<ActivityEvent>();
    foreach(var booking in ChangeTracker.Entries<BusBooking>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray())
      activities.Add(new ActivityEvent(booking.Id,"booking.created",1,booking.CreatedAtUtc,"ebyahe",booking.CreatedBy,
        "Created a bus booking.","/booking-details?reference="+Uri.EscapeDataString(booking.Reference),
        new {booking.Id,booking.Reference,booking.ScheduledTripId,booking.Total,booking.ReservedSeats,booking.Status}));
    foreach(var application in ChangeTracker.Entries<PartnerApplication>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray())
      activities.Add(new ActivityEvent(application.Id,"partner-application.submitted",1,application.SubmittedAtUtc,"ebyahe",application.CreatedBy!,
        "Submitted a partner application.","/partner-application-details/"+application.Id.ToString("D"),
        new {application.Id,application.TrackingId,application.BusinessName,application.BusinessCategory,application.Status}));
    foreach (var entry in ChangeTracker.Entries<Partner>().ToArray())
    {
      if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
      var partner = entry.Entity;
      var oldOwner = entry.State == EntityState.Added ? null : entry.Property(x => x.OwnerUserId).OriginalValue;
      if (partner.OwnerUserId is null && oldOwner is null) continue; // Unowned demo operators have no recipients.
      if (entry.State == EntityState.Modified && !entry.Property(x => x.OwnerUserId).IsModified
        && !entry.Property(x => x.Status).IsModified) continue;
      var created = entry.State == EntityState.Added;
      activities.Add(new ActivityEvent(Guid.NewGuid(), created ? "partner.created" : "partner.updated", 1,
        DateTimeOffset.UtcNow, "ebyahe", "system:ebyahe", created ? "Created a partner." : "Updated a partner.",
        "/my-partner-applications", Audiences: new[] { new { kind = "Administrators" } },
        Partner: new { partner.Id, OwnerExternalUserId = partner.OwnerUserId,
          IsApproved = entry.State != EntityState.Deleted && partner.Status == "Approved" }));
    }
    var events = new List<OutboxMessage>();
    foreach(var activity in activities)
    {
      if(OutboxMessages.Local.Any(x=>x.Id==activity.EventId)) continue;
      var message=ActivityOutbox.Create(activity,kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic);
      OutboxMessages.Add(message); events.Add(message);
    }
    return events;
  }
  private void DetachActivityEvents(IEnumerable<OutboxMessage> events)
  {
    foreach(var message in events) { foreach(var log in message.History.ToArray()) Entry(log).State=EntityState.Detached; Entry(message).State=EntityState.Detached; }
  }
}
