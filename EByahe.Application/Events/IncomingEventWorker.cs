using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;

namespace EByahe.Application.Events;

public sealed class IncomingEventWorker(IEventConsumerFactory consumers, IServiceScopeFactory scopeFactory,
  IEventSettings settings, ILogger<IncomingEventWorker> logger) : BackgroundService
{
  protected override Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!settings.Enabled || !settings.ConsumerEnabled)
    {
      logger.LogInformation("Kafka Account user consumption is disabled.");
      return Task.CompletedTask;
    }
    // Confluent Consume blocks. Keep it off the .NET 9 host startup thread.
    return Task.Run(() => RunAsync(stoppingToken), CancellationToken.None);
  }

  private async Task RunAsync(CancellationToken cancellationToken)
  {
    while (!cancellationToken.IsCancellationRequested)
    {
      IEventConsumer? consumer = null;
      ConsumedRecord? record = null;
      try
      {
        consumer = consumers.Create();
        while (!cancellationToken.IsCancellationRequested)
        {
          record = consumer.Consume(cancellationToken);
          await using var scope = scopeFactory.CreateAsyncScope();
          await scope.ServiceProvider.GetRequiredService<IncomingEventProcessor>()
            .ProcessAsync(consumer, record, cancellationToken);
          logger.LogInformation("Processed Account user event {Topic}/{Partition}/{Offset} for group {Group}.",
            record.Topic, record.Partition, record.Offset, settings.ConsumerGroup);
          record = null;
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
      catch (Exception exception)
      {
        logger.LogError("Kafka user consumer failed at {Topic}/{Partition}/{Offset} with {ErrorType}; rejoining from committed offsets.",
          record?.Topic ?? settings.Topic, record?.Partition, record?.Offset, exception.GetType().Name);
      }
      finally
      {
        if (consumer is not null)
        {
          try { consumer.Close(); }
          catch (Exception exception) { logger.LogWarning("Kafka consumer close failed with {ErrorType}.", exception.GetType().Name); }
          consumer.Dispose();
        }
      }
      try { await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds), cancellationToken); }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
    }
  }
}
