namespace EByahe.Domain.Models;

public sealed class PartnerApplicationHistory
{
  public Guid Id { get; set; }
  public Guid PartnerApplicationId { get; set; }
  public string? PreviousValue { get; set; }
  public string CurrentValue { get; set; } = string.Empty;
  public string Status { get; set; } = string.Empty;
  public DateTimeOffset DateTimeCreated { get; set; }
}
