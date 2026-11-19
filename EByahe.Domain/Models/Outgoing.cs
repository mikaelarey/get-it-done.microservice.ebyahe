namespace EByahe.Domain.Models;

public sealed class Outgoing : ITopicRecord
{
  public Guid Id { get; init; }
  public Guid MessageId { get; init; }
  public int CodeDecodeEventType { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string EventType { get => CodeDecodeValues.Decode("EventTypes", CodeDecodeEventType); init => CodeDecodeEventType = CodeDecodeValues.Code("EventTypes", value); }
  public int CodeDecodeTopic { get; set; }
  [System.Text.Json.Serialization.JsonIgnore]
  public CodeDecodeTopic? TopicSetup { get; set; }
  private string? pendingTopic;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Topic { get => pendingTopic ?? TopicSetup?.Decode ?? string.Empty; init => pendingTopic = value; }
  public bool HasPendingTopic() => pendingTopic is not null;
  public void ClearPendingTopic() => pendingTopic = null;
  public string MessageKey { get; init; } = string.Empty;
  public string Payload { get; init; } = string.Empty;
  public int CodeDecodeOutgoingStatus { get; set; } = 0;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("OutgoingStatuses", CodeDecodeOutgoingStatus); init => CodeDecodeOutgoingStatus = CodeDecodeValues.Code("OutgoingStatuses", value); }
  public DateTime RecordedDateTime { get; init; }
  public DateTime? PublishedDateTime { get; init; }
  public int AttemptCount { get; init; }
  public int? Partition { get; init; }
  public long? Offset { get; init; }
  public string? Error { get; init; }

  public static Outgoing Record(OutboxMessage message, string status, DateTime now,
    int? partition = null, long? offset = null, string? error = null) => new()
  {
    Id = Guid.NewGuid(), MessageId = message.Id, EventType = message.EventType,
    Topic = message.Topic, MessageKey = message.MessageKey, Payload = message.Payload,
    Status = status, RecordedDateTime = now, PublishedDateTime = status == "Published" ? now : null,
    AttemptCount = message.AttemptCount, Partition = partition, Offset = offset, Error = error
  };
}
