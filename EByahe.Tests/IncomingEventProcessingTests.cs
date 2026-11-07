using System.Text.Json;
using EByahe.Application.Events;
using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Messaging;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static ConsumedRecord Registration(long offset = 0, string id = "account-user", string email = "user@example.com") =>
    new("test.account.users", 0, offset, id,
      JsonSerializer.Serialize(new UserRegisteredEvent(Guid.NewGuid(), "user.registered", 1,
        new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero),
        new RegisteredUser(id, "Maria", "Santos", email, "Get It Done", "Normal")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web)));

  private async Task ProcessAsync(IEventConsumer consumer, ConsumedRecord record)
  {
    await using var scope = services!.CreateAsyncScope();
    var processor = new IncomingEventProcessor(scope.ServiceProvider.GetRequiredService<IIncomingRepository>(),
      new KafkaOptions(), TimeProvider.System);
    await processor.ProcessAsync(consumer, record, CancellationToken.None);
  }

  [SqlServerFact]
  public async Task SavesReceiptAndUserBeforeCommittingKafkaOffset()
  {
    var record = Registration();
    using var consumer = new TestConsumer
    {
      OnCommit = () =>
      {
        using var database = CreateDatabase();
        var receipt = database.Incoming.Single();
        Assert.Equal("Processed", receipt.Status);
        Assert.Equal(record.Payload, receipt.Payload);
        Assert.Equal(3, database.IncomingLogs.Count());
        Assert.Equal("Maria", database.RefUsers.Single().FirstName);
      }
    };
    await ProcessAsync(consumer, record);
    await using var database = CreateDatabase();
    var incoming = await database.Incoming.SingleAsync();
    var user = await database.RefUsers.SingleAsync();
    Assert.Equal(1, consumer.Commits);
    Assert.Equal("account-user", user.ExternalUserId);
    Assert.Equal(new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero), user.CreatedAtUtc);
    Assert.NotNull(incoming.MessageId);
    Assert.NotNull(incoming.ProcessedDateTime);
    Assert.Equal(1, incoming.AttemptCount);
    Assert.Equal(new[] { "Acknowledged", "Processed", "Processing", "Received" },
      await database.IncomingLogs.OrderBy(row => row.Status).Select(row => row.Status).ToArrayAsync());
  }

  [SqlServerFact]
  public async Task InvalidMessagesAreDurableAndNeverAcknowledged()
  {
    using var consumer = new TestConsumer();
    var record = Registration() with { Payload = "{broken-json" };
    await Assert.ThrowsAsync<JsonException>(() => ProcessAsync(consumer, record));
    await Assert.ThrowsAsync<JsonException>(() => ProcessAsync(consumer, record));
    await using var database = CreateDatabase();
    var incoming = await database.Incoming.SingleAsync();
    Assert.Equal(record.Payload, incoming.Payload);
    Assert.Equal("Failed", incoming.Status);
    Assert.Equal(2, incoming.AttemptCount);
    Assert.Equal(2, await database.IncomingLogs.CountAsync(row => row.Status == "Failed"));
    Assert.Equal(1, await database.IncomingLogs.CountAsync(row => row.Status == "Received"));
    Assert.Empty(await database.RefUsers.ToListAsync());
    Assert.Equal(0, consumer.Commits);
  }

  [SqlServerFact]
  public async Task DatabaseFailureRollsBackProjectionAndCanRetrySameReceipt()
  {
    await using var database = CreateDatabase();
    database.RefUsers.Add(new RefUser
    {
      ExternalUserId = "other-user", FirstName = "Other", LastName = "User", Email = "user@example.com",
      CreatedAtUtc = DateTimeOffset.UtcNow
    });
    await database.SaveChangesAsync();
    using var consumer = new TestConsumer();
    var record = Registration();
    await Assert.ThrowsAsync<DbUpdateException>(() => ProcessAsync(consumer, record));
    Assert.Equal("Failed", (await database.Incoming.SingleAsync()).Status);
    Assert.Null((await database.Incoming.SingleAsync()).ProcessedDateTime);
    Assert.Equal(0, await database.IncomingLogs.CountAsync(row => row.Status == "Processed"));
    Assert.Equal(0, consumer.Commits);
    Assert.Equal("other-user", (await database.RefUsers.SingleAsync()).ExternalUserId);

    await database.RefUsers.ExecuteDeleteAsync();
    await ProcessAsync(consumer, record);
    database.ChangeTracker.Clear();
    Assert.Equal("account-user", (await database.RefUsers.SingleAsync()).ExternalUserId);
    Assert.Equal("Processed", (await database.Incoming.SingleAsync()).Status);
    Assert.Equal(2, (await database.Incoming.SingleAsync()).AttemptCount);
    Assert.Equal(1, consumer.Commits);
  }

  [SqlServerFact]
  public async Task CommitFailureAndDuplicateEventsNeverOverwriteOrDuplicateUser()
  {
    using var consumer = new TestConsumer { FailCommit = true };
    var record = Registration();
    await Assert.ThrowsAsync<InvalidOperationException>(() => ProcessAsync(consumer, record));
    await using var database = CreateDatabase();
    Assert.Equal("Processed", (await database.Incoming.SingleAsync()).Status);
    Assert.Equal(1, await database.IncomingLogs.CountAsync(row => row.Status == "AcknowledgementFailed"));
    await database.RefUsers.ExecuteUpdateAsync(setters => setters.SetProperty(row => row.FirstName, "Updated"));

    consumer.FailCommit = false;
    await ProcessAsync(consumer, record);
    await ProcessAsync(consumer, record with { Offset = 1 });
    database.ChangeTracker.Clear();
    Assert.Equal("Updated", (await database.RefUsers.SingleAsync()).FirstName);
    Assert.Equal(2, await database.Incoming.CountAsync());
    Assert.All(await database.Incoming.ToListAsync(), row => Assert.Equal(1, row.AttemptCount));
    Assert.Equal(2, consumer.Commits);
  }

  [SqlServerFact]
  public async Task UpgradePreservesLegacyLogsAndConsolidatesRedeliveries()
  {
    await using var database = CreateDatabase();
    var migrator = database.GetService<IMigrator>();
    await ResetToMigrationAsync(database, "20261031041828_InitialRefUsersAndKafkaTables");
    await database.Database.ExecuteSqlRawAsync("""
      DECLARE @delivery uniqueidentifier = NEWID();
      INSERT INTO [Incoming]
        ([Id], [DeliveryId], [ConsumerGroup], [Topic], [Partition], [Offset], [Payload],
         [Status], [ReceivedDateTime], [RecordedDateTime])
      VALUES (NEWID(), @delivery, 'legacy', 'test', 0, 0, N'legacy-payload', 'Received', GETUTCDATE(), GETUTCDATE()),
             (NEWID(), @delivery, 'legacy', 'test', 0, 0, N'legacy-payload', 'Acknowledged', GETUTCDATE(), GETUTCDATE()),
             (NEWID(), NEWID(), 'legacy', 'test', 0, 0, N'legacy-payload', 'Failed', GETUTCDATE(), GETUTCDATE());
      """);
    await migrator.MigrateAsync();
    var incoming = await database.Incoming.SingleAsync();
    Assert.Equal("Received", incoming.Status);
    Assert.Null(incoming.ProcessedDateTime);
    Assert.Equal(3, await database.IncomingLogs.CountAsync());
    Assert.All(await database.IncomingLogs.ToListAsync(), row => Assert.Equal(incoming.Id, row.IncomingId));
  }

  private sealed class TestConsumer : IEventConsumer
  {
    public int Commits { get; private set; }
    public bool FailCommit { get; set; }
    public Action? OnCommit { get; init; }
    public ConsumedRecord Consume(CancellationToken cancellationToken) => throw new NotSupportedException();
    public void Commit(ConsumedRecord record)
    {
      OnCommit?.Invoke();
      if (FailCommit) throw new InvalidOperationException("Test acknowledgement failure.");
      Commits++;
    }
    public void Close() { }
    public void Dispose() { }
  }
}
