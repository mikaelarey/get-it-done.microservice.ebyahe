using System.Text.Json;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Repositories;
using EByahe.Domain.DataModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task EventTypeMigrationPreservesMessagesAndDeliveryHistoryAndRejectsUnknownLegacyTypes()
  {
    await using var db = CreateDatabase(); var now = DateTime.UtcNow;
    var kinds = CodeDecodeValues.Values["EventTypes"];
    var messages = kinds.Select(kind => new OutboxMessage {
      Id = Guid.NewGuid(), UserId = "System", EventType = kind, Topic = "test.events", MessageKey = "System",
      Payload = JsonSerializer.Serialize(new { eventType = kind }), CreatedDateTime = now, NextAttemptDateTime = now
    }).ToArray();
    foreach (var message in messages) message.History.Add(Outgoing.Record(message, "Pending", now));
    db.OutboxMessages.AddRange(messages); await db.SaveChangesAsync();
    var expected = messages.Select(x => new { x.Id, x.UserId, x.EventType, x.Topic, x.MessageKey, x.Payload, x.CreatedDateTime, x.NextAttemptDateTime }).OrderBy(x => x.Id).ToArray();
    var expectedHistory = messages.SelectMany(x => x.History).Select(x => new { x.Id, x.MessageId, x.EventType, x.Topic, x.MessageKey, x.Payload, x.RecordedDateTime, x.Status }).OrderBy(x => x.Id).ToArray();
    db.ChangeTracker.Clear();
    var migrations = db.Database.GetMigrations().ToArray();
    var index = Array.FindIndex(migrations, id => id.EndsWith("_AddEventTypeCodeDecode", StringComparison.Ordinal)); Assert.True(index > 0);
    var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync(migrations[index - 1]);
    Assert.Equal(kinds.Order().ToArray(), (await db.Database.SqlQueryRaw<string>("SELECT [EventType] AS [Value] FROM [OutboxMessages]").ToArrayAsync()).Order().ToArray());
    Assert.Equal(kinds.Order().ToArray(), (await db.Database.SqlQueryRaw<string>("SELECT [EventType] AS [Value] FROM [Outgoing]").ToArrayAsync()).Order().ToArray());
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [OutboxMessages] SET [EventType]='legacy.unknown' WHERE [Id]={messages[0].Id}");
    var invalid = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync()); Assert.Equal(51000, invalid.Number);
    Assert.DoesNotContain(migrations[index], await db.Database.GetAppliedMigrationsAsync());
    Assert.Equal("legacy.unknown", await db.Database.SqlQuery<string>($"SELECT [EventType] AS [Value] FROM [OutboxMessages] WHERE [Id]={messages[0].Id}").SingleAsync());
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [OutboxMessages] SET [EventType]={kinds[0]} WHERE [Id]={messages[0].Id}");
    await migrator.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    Assert.Equal(kinds, await db.CodeDecodeEventTypes.OrderBy(x => x.Code).Select(x => x.Decode).ToArrayAsync());
    var actual = (await db.OutboxMessages.AsNoTracking().ToArrayAsync()).Select(x => new { x.Id, x.UserId, x.EventType, x.Topic, x.MessageKey, x.Payload, x.CreatedDateTime, x.NextAttemptDateTime }).OrderBy(x => x.Id).ToArray();
    var actualHistory = (await db.Outgoing.AsNoTracking().ToArrayAsync()).Select(x => new { x.Id, x.MessageId, x.EventType, x.Topic, x.MessageKey, x.Payload, x.RecordedDateTime, x.Status }).OrderBy(x => x.Id).ToArray();
    Assert.Equal(expected, actual); Assert.Equal(expectedHistory, actualHistory);
    Assert.Single(await db.OutboxMessages.Where(x => x.EventType == "bus.created").ToArrayAsync());
    Assert.Single(await db.Outgoing.Where(x => x.EventType == "bus.created").ToArrayAsync());
    var unknown = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [OutboxMessages] SET [CodeDecodeEventType]=999 WHERE [Id]={messages[0].Id}")); Assert.Equal(547, unknown.Number);
    var unknownHistory = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE [Outgoing] SET [CodeDecodeEventType]=999")); Assert.Equal(547, unknownHistory.Number);
    var used = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM [CodeDecodeEventTypes] WHERE [Code]=1")); Assert.Equal(547, used.Number);
  }

  [SqlServerFact]
  public async Task OutboxDeliveryClaimsRetriesAndCompletesWithTheSameEventTypeCode()
  {
    await using var db = CreateDatabase(); var now = DateTime.UtcNow;
    var message = new OutboxMessage { Id = Guid.NewGuid(), UserId = "System", EventType = "partner.member.requested", Topic = "test.events", MessageKey = "System", Payload = "{}", CreatedDateTime = now, NextAttemptDateTime = now };
    message.History.Add(Outgoing.Record(message, "Pending", now)); db.OutboxMessages.Add(message); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var repository = new OutboxRepository(db);
    var claimed = (await repository.ClaimAsync(now, TimeSpan.FromMinutes(1), default))!;
    Assert.Equal("partner.member.requested", claimed.EventType);
    await repository.FailAsync(claimed, "Test retry", now, now, default); db.ChangeTracker.Clear();
    var retried = (await repository.ClaimAsync(now, TimeSpan.FromMinutes(1), default))!;
    await repository.CompleteAsync(retried, new PublishedRecord(0, 12), now, default); db.ChangeTracker.Clear();
    var saved = await db.OutboxMessages.SingleAsync(); Assert.Equal("Published", saved.Status); Assert.Equal(message.EventType, saved.EventType);
    var history = await db.Outgoing.ToArrayAsync(); Assert.Equal(5, history.Length);
    Assert.All(history, item => { Assert.Equal(message.CodeDecodeEventType, item.CodeDecodeEventType); Assert.Equal(message.EventType, item.EventType); });
  }
}
