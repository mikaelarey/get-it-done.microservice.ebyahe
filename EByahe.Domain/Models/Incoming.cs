namespace EByahe.Domain.Models;

public sealed class Incoming
{
  public Guid Id { get; init; }
  public Guid DeliveryId { get; init; }
  public Guid? MessageId { get; set; }
  public string ConsumerGroup { get; init; } = string.Empty;
  public string Topic { get; init; } = string.Empty;
  public int Partition { get; init; }
  public long Offset { get; init; }
  public string? MessageKey { get; init; }
  public string? Payload { get; init; }
  public string Status { get; set; } = "Received";
  public DateTime ReceivedDateTime { get; init; }
  public DateTime RecordedDateTime { get; set; }
  public DateTime? ProcessedDateTime { get; set; }
  public int AttemptCount { get; set; }
  public string? Error { get; set; }
  public ICollection<IncomingLog> Logs { get; set; } = new List<IncomingLog>();
}
