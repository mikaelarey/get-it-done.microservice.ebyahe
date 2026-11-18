using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Messaging;

internal static class PartnerCatalogActivityOutbox
{
  public static Task QueueMembershipUpdateAsync(EByaheDbContext db, Guid partnerId, string actor, Guid membershipId,
    bool previousIsActive, bool isActive, string topic, CancellationToken ct) =>
    QueueActivityAsync(db, partnerId, actor, "partner-user.updated", "Updated a partner user.", "/users",
      new { id = membershipId, partnerId, previousIsActive, isActive }, topic, ct);

  public static Task QueueAsync(EByaheDbContext db, Guid partnerId, string actor, string type, Guid entityId,
    string topic, CancellationToken cancellationToken)
  {
    var (message, path) = type switch
    {
      "bus.created" => ("Created a bus.", "/buses"),
      "bus.updated" => ("Updated a bus.", "/buses"),
      "terminal.created" => ("Created a terminal.", "/terminals"),
      "terminal.updated" => ("Updated a terminal.", "/terminals"),
      "scheduled-trip.created" => ("Created a scheduled trip.", "/scheduled-trips"),
      "scheduled-trip.updated" => ("Updated a scheduled trip.", "/scheduled-trips"),
      _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    return QueueActivityAsync(db, partnerId, actor, type, message, path, new { id = entityId, partnerId }, topic, cancellationToken);
  }

  public static Task QueueImportAsync(EByaheDbContext db, Guid partnerId, string actor, string type, int recordCount, Guid uploadId,
    string topic, CancellationToken cancellationToken)
  {
    if (recordCount is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(recordCount));
    var noun = type switch
    {
      "buses.imported" => recordCount == 1 ? "bus" : "buses",
      "terminals.imported" => recordCount == 1 ? "terminal" : "terminals",
      "scheduled-trips.imported" => recordCount == 1 ? "scheduled trip" : "scheduled trips",
      "users.imported" => recordCount == 1 ? "user" : "users",
      _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    return QueueActivityAsync(db, partnerId, actor, type, $"Successfully created {recordCount} {noun} using file upload.", $"/uploads/{uploadId:D}",
      new { id = uploadId, partnerId, recordCount, uploadId }, topic, cancellationToken);
  }

  private static async Task QueueActivityAsync(EByaheDbContext db, Guid partnerId, string actor, string type,
    string message, string path, object details, string topic, CancellationToken cancellationToken)
  {
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
        message, path, Details: details,
        Audiences: batch.Select(subject => new { kind = "User", externalUserId = subject }).ToArray()), topic));
  }
}
