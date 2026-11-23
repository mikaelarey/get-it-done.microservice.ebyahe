using EByahe.Infrastructure.Messaging;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EByahe.Application.Services;

// Repairs pre-deployment bookings and an accidentally missing registration, without changing the original deadline.
public sealed class CashBookingRegistrationWorker(IServiceScopeFactory scopes, IOptions<KafkaOptions> kafka,
  ILogger<CashBookingRegistrationWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    if (!kafka.Value.Enabled) return;
    await Task.Yield();
    while (!stoppingToken.IsCancellationRequested) {
      try {
        await using var scope = scopes.CreateAsyncScope();
        await QueueMissingAsync(scope.ServiceProvider.GetRequiredService<EByaheDbContext>(), kafka.Value.CashBookingsTopic, stoppingToken);
      } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        catch (Exception error) { logger.LogWarning("Cash booking deadline registration failed with {ErrorType}; it will retry.", error.GetType().Name); }
      try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
    }
  }
  public static async Task QueueMissingAsync(EByaheDbContext db, string topic, CancellationToken ct)
  {
    var ids = await db.BusBookings.FromSqlRaw("SELECT * FROM [BusBookings] WHERE JSON_VALUE([DetailsJson], '$.payment.method') = 'cash'")
      .AsNoTracking().Where(booking => booking.Status == "Submitted" && booking.PaymentStatus != "Payment Success"
        && booking.PaymentStatus != "Cancelled" && booking.ScheduledTripId != null
        && !db.OutboxMessages.Any(message => message.TopicSetup!.Decode == topic && message.MessageKey == booking.Id.ToString()))
      .OrderBy(booking => booking.CreatedAtUtc).Select(booking => booking.Id).Take(100).ToArrayAsync(ct);
    foreach (var id in ids) {
      db.ChangeTracker.Clear();
      await using var transaction = await db.Database.BeginTransactionAsync(ct);
      var booking = await db.BusBookings.FromSqlInterpolated($"SELECT * FROM [BusBookings] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleAsync(ct);
      if (booking.Status == "Submitted" && booking.PaymentStatus is not ("Payment Success" or "Cancelled")
        && !await db.OutboxMessages.AnyAsync(message => message.TopicSetup!.Decode == topic && message.MessageKey == id.ToString(), ct)) {
        var partnerId = await db.ScheduledTrips.Where(trip => trip.Id == booking.ScheduledTripId).Select(trip => trip.PartnerId).SingleAsync(ct);
        CashBookingDeadlineOutbox.Queue(db, booking, partnerId, topic);
        await db.SaveChangesAsync(ct);
      }
      await transaction.CommitAsync(ct);
    }
  }
}
