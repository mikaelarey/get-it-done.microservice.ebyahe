namespace EByahe.Domain.Models;

public sealed class Incoming : ITopicRecord
{
  public Guid Id { get; init; }
  public Guid DeliveryId { get; init; }
  public Guid? MessageId { get; set; }
  public string ConsumerGroup { get; init; } = string.Empty;
  public int CodeDecodeTopic { get; set; }
  [System.Text.Json.Serialization.JsonIgnore]
  public CodeDecodeTopic? TopicSetup { get; set; }
  private string? pendingTopic;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Topic { get => pendingTopic ?? TopicSetup?.Decode ?? string.Empty; init => pendingTopic = value; }
  public bool HasPendingTopic() => pendingTopic is not null;
  public void ClearPendingTopic() => pendingTopic = null;
  public int Partition { get; init; }
  public long Offset { get; init; }
  public string? MessageKey { get; init; }
  public string? Payload { get; init; }
  public int CodeDecodeIncomingStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("IncomingStatuses", CodeDecodeIncomingStatus); set => CodeDecodeIncomingStatus = CodeDecodeValues.Code("IncomingStatuses", value); }
  public DateTime ReceivedDateTime { get; init; }
  public DateTime RecordedDateTime { get; set; }
  public DateTime? ProcessedDateTime { get; set; }
  public int AttemptCount { get; set; }
  public string? Error { get; set; }
  public ICollection<IncomingLog> Logs { get; set; } = new List<IncomingLog>();
}
