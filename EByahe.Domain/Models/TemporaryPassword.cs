namespace EByahe.Domain.Models;
public sealed class TemporaryPassword
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid RequestId { get; set; }
  public PartnerMemberRequest Request { get; set; } = null!;
  public string ProtectedPassword { get; set; } = "";
  public bool CanReveal { get; set; }
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
  public DateTimeOffset AvailableUntilUtc { get; set; }
}
