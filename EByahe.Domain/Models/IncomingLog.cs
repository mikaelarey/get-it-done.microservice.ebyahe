namespace EByahe.Domain.Models;

public sealed class IncomingLog
{
  public Guid Id { get; init; }
  public Guid IncomingId { get; init; }
  public Guid DeliveryId { get; init; }
  public int AttemptCount { get; init; }
  public string Status { get; init; } = string.Empty;
  public DateTime RecordedDateTime { get; init; }
  public string? Error { get; init; }
}
