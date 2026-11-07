using EByahe.Domain.DataModels;
using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IOutboxRepository
{
  void Add(OutboxMessage message);
  Task<OutboxMessage?> ClaimAsync(DateTime now, TimeSpan lease, CancellationToken cancellationToken);
  Task CompleteAsync(OutboxMessage message, PublishedRecord result, DateTime now, CancellationToken cancellationToken);
  Task FailAsync(OutboxMessage message, string error, DateTime now, DateTime retryAt, CancellationToken cancellationToken);
}
