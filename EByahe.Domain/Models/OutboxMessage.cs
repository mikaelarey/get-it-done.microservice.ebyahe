namespace EByahe.Domain.Models;

// Operational delivery state. Outgoing contains the immutable status history.
public sealed class OutboxMessage
{
  public Guid Id { get; set; }
  public string UserId { get; set; } = string.Empty;
  public string EventType { get; set; } = string.Empty;
  public string Topic { get; set; } = string.Empty;
  public string MessageKey { get; set; } = string.Empty;
  public string Payload { get; set; } = string.Empty;
  public string Status { get; set; } = "Pending";
  public DateTime CreatedDateTime { get; set; }
  public DateTime NextAttemptDateTime { get; set; }
  public int AttemptCount { get; set; }
  public Guid? LockToken { get; set; }
  public DateTime? LockedUntil { get; set; }
  public ICollection<Outgoing> History { get; set; } = new List<Outgoing>();
}
