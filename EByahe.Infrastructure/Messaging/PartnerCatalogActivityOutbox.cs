using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Messaging;

internal static class PartnerCatalogActivityOutbox
{
  public static async Task QueueAsync(EByaheDbContext db, Guid partnerId, string actor, string type, Guid entityId,
    string topic, CancellationToken cancellationToken)
  {
    var (message, path) = type switch
    {
      "bus.created" => ("Created a bus.", "/buses"),
      "terminal.created" => ("Created a terminal.", "/terminals"),
      "scheduled-trip.created" => ("Created a scheduled trip.", "/scheduled-trips"),
      _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    // Snapshot the selected partner, not all of the actor's workspaces. Do not use
    // the generic Partner audience, which currently expands only to its owner.
    var subjects = await db.PartnerUsers.AsNoTracking().Where(member => member.PartnerId == partnerId
      && member.IsActive && member.Partner.Status == "Approved")
      .Select(member => member.RefUser.ExternalUserId).Distinct().OrderBy(subject => subject).ToArrayAsync(cancellationToken);
    if (subjects.Length == 0) throw new InvalidOperationException("Partner activity requires active recipients.");
    var now = DateTimeOffset.UtcNow;
    // Small disjoint batches also fit the 64 KiB contract at maximum subject length.
    foreach (var batch in subjects.Chunk(25))
      db.OutboxMessages.Add(ActivityOutbox.Create(new ActivityEvent(Guid.NewGuid(), type, 1, now, "ebyahe", actor,
        message, path, Details: new { id = entityId, partnerId },
        Audiences: batch.Select(subject => new { kind = "User", externalUserId = subject }).ToArray()), topic));
  }
}
