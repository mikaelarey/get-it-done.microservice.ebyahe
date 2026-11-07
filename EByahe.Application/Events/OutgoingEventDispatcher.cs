using EByahe.Domain.Interfaces;
using EByahe.Domain.Repositories;

namespace EByahe.Application.Events;

public sealed class OutgoingEventDispatcher(IOutboxRepository outbox, IEventPublisher publisher,
  IEventSettings settings, TimeProvider clock, ILogger<OutgoingEventDispatcher> logger)
{
  public async Task<bool> DispatchNextAsync(CancellationToken cancellationToken)
  {
    var message = await outbox.ClaimAsync(clock.GetUtcNow().UtcDateTime,
      TimeSpan.FromSeconds(settings.LeaseSeconds), cancellationToken);
    if (message is null) return false;
    try
    {
      var result = await publisher.PublishAsync(message, cancellationToken);
      await outbox.CompleteAsync(message, result, clock.GetUtcNow().UtcDateTime, cancellationToken);
      logger.LogInformation("Published event {MessageId} to {Topic}/{Partition}/{Offset} on attempt {Attempt}.",
        message.Id, message.Topic, result.Partition, result.Offset, message.AttemptCount);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // The lease expires after an interrupted delivery. Retrying uses the same event ID.
      throw;
    }
    catch (Exception exception)
    {
      var now = clock.GetUtcNow().UtcDateTime;
      var delay = Math.Min(3600, settings.RetryDelaySeconds * Math.Pow(2, Math.Min(message.AttemptCount - 1, 10)));
      // Do not persist arbitrary exception text, which might contain payloads or credentials.
      var error = exception is EByahe.Domain.Exceptions.EventTransportException
        ? exception.Message : "Delivery or confirmation persistence failed; retry required.";
      await outbox.FailAsync(message, error, now, now.AddSeconds(delay), cancellationToken);
      logger.LogWarning("Event {MessageId} delivery failed on attempt {Attempt}; retry scheduled. Error type: {ErrorType}.",
        message.Id, message.AttemptCount, exception.GetType().Name);
    }
    return true;
  }
}
