namespace EByahe.Domain.Models;

public sealed class PartnerApplicationHistory
{
  public Guid Id { get; set; }
  public Guid PartnerApplicationId { get; set; }
  public string? PreviousValue { get; set; }
  public string CurrentValue { get; set; } = string.Empty;
  public int CodeDecodePartnerApplicationStatus { get; set; } = 0;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("PartnerApplicationStatuses", CodeDecodePartnerApplicationStatus); set => CodeDecodePartnerApplicationStatus = CodeDecodeValues.Code("PartnerApplicationStatuses", value); }
  public DateTimeOffset DateTimeCreated { get; set; }
}
