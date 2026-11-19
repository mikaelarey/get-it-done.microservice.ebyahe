using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
namespace EByahe.Infrastructure.Persistence;
public sealed partial class EByaheDbContext
{
  private void DetachTopicSetups()
  {
    foreach (var entry in ChangeTracker.Entries<CodeDecodeTopic>().ToArray()) entry.State = EntityState.Detached;
  }
  private ITopicRecord[] PendingTopicRecords() => ChangeTracker.Entries().Select(entry => entry.Entity).OfType<ITopicRecord>().Where(record => record.HasPendingTopic()).ToArray();
  private bool HasTopicChanges() => PendingTopicRecords().Length > 0
    || ChangeTracker.Entries<Partner>().Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
    || ChangeTracker.Entries<BusBooking>().Any(entry => entry.State == EntityState.Added);
  private static void ValidateTopic(string name)
  {
    if (name.Length is < 1 or > 249 || name is "." or ".." || !Regex.IsMatch(name, @"\A[a-zA-Z0-9._-]+\z"))
      throw new InvalidOperationException("Use a valid configured Kafka topic name.");
  }
  private void ResolveTopics(ITopicRecord[] records)
  {
    foreach (var name in records.Select(record => record.Topic).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
    {
      ValidateTopic(name);
      var setup = CodeDecodeTopics.Local.SingleOrDefault(topic => topic.Decode == name);
      if (setup is null) {
        Database.ExecuteSqlInterpolated($"""
          INSERT INTO [CodeDecodeTopics] ([Id],[Decode],[CreatedBy])
          SELECT NEWID(), {name}, 'System' WHERE NOT EXISTS
            (SELECT 1 FROM [CodeDecodeTopics] WITH (UPDLOCK,HOLDLOCK) WHERE [Decode]={name});
          """);
        setup = CodeDecodeTopics.Single(topic => topic.Decode == name);
      }
      foreach (var record in records.Where(record => record.Topic == name)) { record.TopicSetup = setup; record.CodeDecodeTopic = setup.Code; }
    }
  }
  private async Task ResolveTopicsAsync(ITopicRecord[] records, CancellationToken ct)
  {
    foreach (var name in records.Select(record => record.Topic).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
    {
      ValidateTopic(name);
      var setup = CodeDecodeTopics.Local.SingleOrDefault(topic => topic.Decode == name);
      if (setup is null) {
        await Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO [CodeDecodeTopics] ([Id],[Decode],[CreatedBy])
          SELECT NEWID(), {name}, 'System' WHERE NOT EXISTS
            (SELECT 1 FROM [CodeDecodeTopics] WITH (UPDLOCK,HOLDLOCK) WHERE [Decode]={name});
          """, ct);
        setup = await CodeDecodeTopics.SingleAsync(topic => topic.Decode == name, ct);
      }
      foreach (var record in records.Where(record => record.Topic == name)) { record.TopicSetup = setup; record.CodeDecodeTopic = setup.Code; }
    }
  }
}
