using EByahe.Domain.Interfaces;

namespace EByahe.Application.Events;

public sealed class OutgoingEventWorker(IServiceScopeFactory scopeFactory, IEventSettings settings,
  ILogger<OutgoingEventWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!settings.Enabled)
    {
      logger.LogInformation("Kafka publishing is disabled; activity events remain pending in the outbox.");
      return;
    }
    await Task.Yield();
    while (!stoppingToken.IsCancellationRequested)
    {
      var processed = false;
      try
      {
        await using var scope = scopeFactory.CreateAsyncScope();
        processed = await scope.ServiceProvider.GetRequiredService<OutgoingEventDispatcher>().DispatchNextAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
      catch (Exception exception)
      {
        logger.LogError("Outbox worker failed with {ErrorType}; pending events will be retried.", exception.GetType().Name);
      }
      if (!processed)
      {
        try { await Task.Delay(settings.PollIntervalMilliseconds, stoppingToken); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
      }
    }
  }
}
