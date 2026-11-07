namespace EByahe.Domain.Models;

public sealed class PartnerUser
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid PartnerId { get; set; }
  public Partner Partner { get; set; } = null!;
  public Guid RefUserId { get; set; }
  public RefUser RefUser { get; set; } = null!;
  public bool IsActive { get; set; } = true;
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
