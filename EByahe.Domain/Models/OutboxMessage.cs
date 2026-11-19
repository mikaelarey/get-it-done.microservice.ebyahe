namespace EByahe.Domain.Models;

// Operational delivery state. Outgoing contains the immutable status history.
public sealed class OutboxMessage : ITopicRecord
{
  public Guid Id { get; set; }
  public string UserId { get; set; } = string.Empty;
  public int CodeDecodeEventType { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string EventType { get => CodeDecodeValues.Decode("EventTypes", CodeDecodeEventType); set => CodeDecodeEventType = CodeDecodeValues.Code("EventTypes", value); }
  public int CodeDecodeTopic { get; set; }
  [System.Text.Json.Serialization.JsonIgnore]
  public CodeDecodeTopic? TopicSetup { get; set; }
  private string? pendingTopic;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Topic { get => pendingTopic ?? TopicSetup?.Decode ?? string.Empty; set => pendingTopic = value; }
  public bool HasPendingTopic() => pendingTopic is not null;
  public void ClearPendingTopic() => pendingTopic = null;
  public string MessageKey { get; set; } = string.Empty;
  public string Payload { get; set; } = string.Empty;
  public int CodeDecodeOutboxStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("OutboxStatuses", CodeDecodeOutboxStatus); set => CodeDecodeOutboxStatus = CodeDecodeValues.Code("OutboxStatuses", value); }
  public DateTime CreatedDateTime { get; set; }
  public DateTime NextAttemptDateTime { get; set; }
  public int AttemptCount { get; set; }
  public Guid? LockToken { get; set; }
  public DateTime? LockedUntil { get; set; }
  public ICollection<Outgoing> History { get; set; } = new List<Outgoing>();
}
