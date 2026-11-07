using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class OutboxRepository(EByaheDbContext database) : IOutboxRepository
{
  public void Add(OutboxMessage message) => database.OutboxMessages.Add(message);

  public async Task<OutboxMessage?> ClaimAsync(DateTime now, TimeSpan lease, CancellationToken cancellationToken)
  {
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    var eligible = database.OutboxMessages.Where(row =>
      ((row.Status == "Pending" || row.Status == "Failed") && row.NextAttemptDateTime <= now)
      || (row.Status == "Publishing" && row.LockedUntil <= now));
    var candidate = await eligible.AsNoTracking().OrderBy(row => row.CreatedDateTime).ThenBy(row => row.Id)
      .FirstOrDefaultAsync(cancellationToken);
    if (candidate is null) return null;
    var token = Guid.NewGuid();
    var until = now.Add(lease);
    var claimed = await eligible.Where(row => row.Id == candidate.Id && row.AttemptCount == candidate.AttemptCount)
      .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, "Publishing")
        .SetProperty(row => row.AttemptCount, row => row.AttemptCount + 1)
        .SetProperty(row => row.LockToken, token).SetProperty(row => row.LockedUntil, until), cancellationToken);
    if (claimed == 0) return null;
    if (candidate.Status == "Publishing")
      database.Outgoing.Add(Outgoing.Record(candidate, "LeaseExpired", now));
    candidate.Status = "Publishing";
    candidate.AttemptCount++;
    candidate.LockToken = token;
    candidate.LockedUntil = until;
    database.Outgoing.Add(Outgoing.Record(candidate, "Publishing", now));
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return candidate;
  }

  public async Task CompleteAsync(OutboxMessage message, PublishedRecord result, DateTime now,
    CancellationToken cancellationToken)
  {
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    try
    {
      var changed = await Owned(message).ExecuteUpdateAsync(setters => setters
        .SetProperty(row => row.Status, "Published").SetProperty(row => row.LockToken, (Guid?)null)
        .SetProperty(row => row.LockedUntil, (DateTime?)null), cancellationToken);
      RequireOwnership(changed);
      database.Outgoing.Add(Outgoing.Record(message, "Published", now, result.Partition, result.Offset));
      await database.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
    }
    catch
    {
      // A failed confirmation must not leave an Added log for the next operation to save.
      database.ChangeTracker.Clear();
      throw;
    }
  }

  public async Task FailAsync(OutboxMessage message, string error, DateTime now, DateTime retryAt,
    CancellationToken cancellationToken)
  {
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    var changed = await Owned(message).ExecuteUpdateAsync(setters => setters
      .SetProperty(row => row.Status, "Failed").SetProperty(row => row.NextAttemptDateTime, retryAt)
      .SetProperty(row => row.LockToken, (Guid?)null).SetProperty(row => row.LockedUntil, (DateTime?)null), cancellationToken);
    RequireOwnership(changed);
    database.Outgoing.Add(Outgoing.Record(message, "Failed", now, error: error));
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }

  private IQueryable<OutboxMessage> Owned(OutboxMessage message) => database.OutboxMessages
    .Where(row => row.Id == message.Id && row.Status == "Publishing" && row.LockToken == message.LockToken);

  private static void RequireOwnership(int changed)
  {
    if (changed != 1) throw new InvalidOperationException("The outbox delivery lease no longer belongs to this worker.");
  }
}
