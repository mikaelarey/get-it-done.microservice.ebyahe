using EByahe.Domain.Models;
using EByahe.Domain.DataModels;
using EByahe.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace EByahe.Tests;
public sealed partial class SqlServerDatabaseTests
{
  private static OutboxMessage TopicMessage(string topic) => new() { Id = Guid.NewGuid(), UserId = "System", EventType = "bus.created", Topic = topic, MessageKey = "System", Payload = "{\"example\":true}", CreatedDateTime = DateTime.UtcNow, NextAttemptDateTime = DateTime.UtcNow };

  [SqlServerFact]
  public async Task TopicLookupMigrationPreservesMessagesHistoryReceiptsAndCaseSensitiveTopicNames()
  {
    await using var db = CreateDatabase(); var now = DateTime.UtcNow;
    var names = new[] { "custom.Case.v1", "custom.case.v1" };
    foreach (var name in names) {
      var message = TopicMessage(name); message.History.Add(Outgoing.Record(message, "Pending", now)); db.OutboxMessages.Add(message);
      db.Incoming.Add(new Incoming { Id = Guid.NewGuid(), DeliveryId = Guid.NewGuid(), Topic = name, ConsumerGroup = "topic-test", Partition = 0, Offset = Array.IndexOf(names, name), Payload = "test", ReceivedDateTime = now, RecordedDateTime = now });
    }
    await db.SaveChangesAsync();
    var expected = await db.OutboxMessages.OrderBy(x => x.Id).Select(x => new { x.Id, x.Topic, x.Payload, x.MessageKey, x.Status }).ToArrayAsync();
    var history = await db.Outgoing.OrderBy(x => x.Id).Select(x => new { x.Id, x.MessageId, x.Topic, x.Payload, x.Status }).ToArrayAsync();
    var receipts = await db.Incoming.OrderBy(x => x.Id).Select(x => new { x.Id, x.DeliveryId, x.Topic, x.ConsumerGroup, x.Partition, x.Offset, x.Payload }).ToArrayAsync();
    db.ChangeTracker.Clear(); var migrations = db.Database.GetMigrations().ToArray(); var index = Array.FindIndex(migrations, id => id.EndsWith("_AddTopicCodeDecode", StringComparison.Ordinal)); Assert.True(index > 0);
    var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(migrations[index - 1]);
    Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM [OutboxMessages] WHERE [Topic] LIKE 'custom.%'").SingleAsync());
    await migrator.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    Assert.Equal(expected, await db.OutboxMessages.OrderBy(x => x.Id).Select(x => new { x.Id, x.Topic, x.Payload, x.MessageKey, x.Status }).ToArrayAsync());
    Assert.Equal(history, await db.Outgoing.OrderBy(x => x.Id).Select(x => new { x.Id, x.MessageId, x.Topic, x.Payload, x.Status }).ToArrayAsync());
    Assert.Equal(receipts, await db.Incoming.OrderBy(x => x.Id).Select(x => new { x.Id, x.DeliveryId, x.Topic, x.ConsumerGroup, x.Partition, x.Offset, x.Payload }).ToArrayAsync());
    Assert.Equal(2, await db.CodeDecodeTopics.CountAsync(x => names.Contains(x.Decode)));
    foreach (var table in new[] { "OutboxMessages", "Outgoing", "Incoming" }) {
      var invalid = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE [" + table + "] SET [CodeDecodeTopic]=999999")); Assert.Equal(547, invalid.Number);
    }
    var receipt = receipts[0]; var same = await new IncomingRepository(db).ReceiveAsync(receipt.ConsumerGroup, new ConsumedRecord(receipt.Topic, receipt.Partition, receipt.Offset, null, receipt.Payload!), now, default);
    Assert.Equal(receipt.Id, same); Assert.Equal(2, await db.Incoming.CountAsync());
  }

  [SqlServerFact]
  public async Task NewTopicRegistrationIsAtomicAndCanRetryAfterAnOutboxFailure()
  {
    await using var db = CreateDatabase(); var message = TopicMessage("rollback.topic.v1"); db.OutboxMessages.Add(message);
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTopicRecord ON dbo.OutboxMessages AFTER INSERT AS THROW 51098,'Test topic rollback',1;");
    await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    Assert.False(await db.CodeDecodeTopics.AsNoTracking().AnyAsync(x => x.Decode == "rollback.topic.v1"));
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectTopicRecord"); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    Assert.Equal("rollback.topic.v1", (await db.OutboxMessages.SingleAsync()).Topic);
    Assert.Equal("System", (await db.CodeDecodeTopics.SingleAsync(x => x.Decode == "rollback.topic.v1")).CreatedBy);
  }

  [SqlServerFact]
  public async Task ConcurrentTopicRegistrationReusesOneCode()
  {
    async Task Add() { await using var db = CreateDatabase(); db.OutboxMessages.Add(TopicMessage("concurrent.topic.v1")); await db.SaveChangesAsync(); }
    await Task.WhenAll(Add(), Add()); await using var verify = CreateDatabase();
    var topic = await verify.CodeDecodeTopics.SingleAsync(x => x.Decode == "concurrent.topic.v1");
    Assert.Equal(2, await verify.OutboxMessages.CountAsync(x => x.CodeDecodeTopic == topic.Code));
  }
}
