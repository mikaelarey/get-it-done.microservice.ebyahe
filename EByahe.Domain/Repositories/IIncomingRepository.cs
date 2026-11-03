using EByahe.Domain.DataModels;
using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IIncomingRepository
{
  Task<Guid> ReceiveAsync(string consumerGroup, ConsumedRecord record, DateTime now, CancellationToken cancellationToken);
  Task<bool> BeginProcessingAsync(Guid incomingId, DateTime now, CancellationToken cancellationToken);
  Task CompleteAsync(Guid incomingId, Guid messageId, RefUser user, DateTime now, CancellationToken cancellationToken);
  Task FailAsync(Guid incomingId, string error, DateTime now, CancellationToken cancellationToken);
  Task AppendLogAsync(Guid incomingId, string status, string? error, DateTime now, CancellationToken cancellationToken);
}
