using System.Data;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class IncomingRepository(EByaheDbContext database) : IIncomingRepository
{
  public async Task<Guid> ReceiveAsync(string consumerGroup, ConsumedRecord record, DateTime now,
    CancellationToken cancellationToken)
  {
    database.ChangeTracker.Clear();
    var receipts = database.Incoming.Where(row => row.ConsumerGroup == consumerGroup && row.Topic == record.Topic
      && row.Partition == record.Partition && row.Offset == record.Offset);
    var existing = await receipts.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
    if (existing is not null) return existing.Id;

    var receipt = new Incoming
    {
      Id = Guid.NewGuid(), DeliveryId = Guid.NewGuid(), ConsumerGroup = consumerGroup,
      Topic = record.Topic, Partition = record.Partition, Offset = record.Offset,
      MessageKey = record.Key, Payload = record.Payload,
      ReceivedDateTime = now, RecordedDateTime = now
    };
    receipt.Logs.Add(Log(receipt, "Received", now));
    database.Incoming.Add(receipt);
    try { await database.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
    {
      // A rebalance can briefly overlap deliveries to two service instances.
      database.ChangeTracker.Clear();
      return await receipts.Select(row => row.Id).SingleAsync(cancellationToken);
    }
    return receipt.Id;
  }

  public async Task<bool> BeginProcessingAsync(Guid incomingId, DateTime now, CancellationToken cancellationToken)
  {
    database.ChangeTracker.Clear();
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var receipt = await LockReceiptAsync(incomingId, cancellationToken);
    if (receipt.Status == "Processed") return false;
    receipt.AttemptCount++;
    receipt.Status = "Processing";
    receipt.Error = null;
    receipt.RecordedDateTime = now;
    database.IncomingLogs.Add(Log(receipt, "Processing", now));
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return true;
  }

  public async Task CompleteAsync(Guid incomingId, Guid messageId, RefUser user, DateTime now,
    CancellationToken cancellationToken)
  {
    database.ChangeTracker.Clear();
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var receipt = await LockReceiptAsync(incomingId, cancellationToken);
    if (receipt.Status == "Processed") return;

    // Registration is insert-only. Replays must not overwrite later profile changes.
    // Range locking also serializes different Kafka records for the same user ID.
    var existing = await database.RefUsers.FromSqlInterpolated(
      $"SELECT * FROM [RefUsers] WITH (UPDLOCK, HOLDLOCK) WHERE [ExternalUserId] = {user.ExternalUserId}")
      .SingleOrDefaultAsync(cancellationToken);
    if (existing is null) database.RefUsers.Add(user);
    receipt.MessageId = messageId;
    receipt.Status = "Processed";
    receipt.Error = null;
    receipt.RecordedDateTime = now;
    receipt.ProcessedDateTime = now;
    database.IncomingLogs.Add(Log(receipt, "Processed", now));
    // User projection and completion marker either both commit or both roll back.
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }

  public async Task FailAsync(Guid incomingId, string error, DateTime now, CancellationToken cancellationToken)
  {
    await SetRecordState(incomingId, null, "Failed", error, now, cancellationToken);
  }

  public Task CompleteRecordAsync(Guid incomingId, Guid messageId, DateTime now, CancellationToken cancellationToken) =>
    SetRecordState(incomingId, messageId, "Processed", null, now, cancellationToken);

  private async Task SetRecordState(Guid incomingId, Guid? messageId, string status, string? error, DateTime now, CancellationToken cancellationToken)
  {
    database.ChangeTracker.Clear();
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var receipt = await LockReceiptAsync(incomingId, cancellationToken);
    if (receipt.Status == "Processed") return;
    receipt.Status = status;
    if (messageId is not null) { receipt.MessageId = messageId; receipt.ProcessedDateTime = now; }
    receipt.Error = error;
    receipt.RecordedDateTime = now;
    database.IncomingLogs.Add(Log(receipt, status, now, error));
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
  }

  public async Task AppendLogAsync(Guid incomingId, string status, string? error, DateTime now,
    CancellationToken cancellationToken)
  {
    database.ChangeTracker.Clear();
    var receipt = await database.Incoming.AsNoTracking().SingleAsync(row => row.Id == incomingId, cancellationToken);
    database.IncomingLogs.Add(Log(receipt, status, now, error));
    await database.SaveChangesAsync(cancellationToken);
  }

  private Task<Incoming> LockReceiptAsync(Guid incomingId, CancellationToken cancellationToken) =>
    database.Incoming.FromSqlInterpolated(
      $"SELECT * FROM [Incoming] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {incomingId}")
      .SingleAsync(cancellationToken);

  private static IncomingLog Log(Incoming receipt, string status, DateTime now, string? error = null) => new()
  {
    Id = Guid.NewGuid(), IncomingId = receipt.Id, DeliveryId = receipt.DeliveryId,
    AttemptCount = receipt.AttemptCount, Status = status, RecordedDateTime = now, Error = error
  };
}
