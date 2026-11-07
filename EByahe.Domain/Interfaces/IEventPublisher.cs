using EByahe.Domain.DataModels;
using EByahe.Domain.Models;

namespace EByahe.Domain.Interfaces;

public interface IEventPublisher
{
  Task<PublishedRecord> PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
