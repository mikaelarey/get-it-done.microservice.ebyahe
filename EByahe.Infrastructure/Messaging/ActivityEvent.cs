using System.Text.Json;
using EByahe.Domain.Models;
namespace EByahe.Infrastructure.Messaging;

public sealed record ActivityEvent(Guid EventId, string EventType, int SchemaVersion, DateTimeOffset OccurredAtUtc,
  string Source, string CreatedBy, string Message, string Link, object? Details = null, object? User = null,
  object? Audiences = null, object? Partner = null);

internal static class ActivityOutbox
{
  public const string DefaultTopic = "get-it-done.notifications.v1";
  public static OutboxMessage Create(ActivityEvent activity, string topic, string? key = null)
  {
    var now=activity.OccurredAtUtc.UtcDateTime;
    var message=new OutboxMessage { Id=activity.EventId,UserId=activity.CreatedBy,EventType=activity.EventType,
      Topic=topic,MessageKey=key ?? activity.CreatedBy,CreatedDateTime=now,NextAttemptDateTime=now,
      Payload=JsonSerializer.Serialize(activity,new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
    message.History.Add(Outgoing.Record(message,"Pending",now));
    return message;
  }
}
