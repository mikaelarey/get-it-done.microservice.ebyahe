namespace EByahe.Domain.Models;

public sealed class IncomingLog
{
  public Guid Id { get; init; }
  public Guid IncomingId { get; init; }
  public Guid DeliveryId { get; init; }
  public int AttemptCount { get; init; }
  public int CodeDecodeIncomingStatus { get; set; } = 0;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("IncomingStatuses", CodeDecodeIncomingStatus); init => CodeDecodeIncomingStatus = CodeDecodeValues.Code("IncomingStatuses", value); }
  public DateTime RecordedDateTime { get; init; }
  public string? Error { get; init; }
}
