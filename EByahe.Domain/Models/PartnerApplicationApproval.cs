namespace EByahe.Domain.Models;

/** One immutable entry per approval status transition. The latest entry is the current approval state. */
public sealed class PartnerApplicationApproval
{
  public Guid Id { get; set; }
  public Guid PartnerApplicationId { get; set; }
  public int? CodeDecodePreviousPartnerApplicationApprovalStatus { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string? PreviousStatus { get => CodeDecodePreviousPartnerApplicationApprovalStatus is { } code ? CodeDecodeValues.Decode("PartnerApplicationApprovalStatuses", code) : null; set => CodeDecodePreviousPartnerApplicationApprovalStatus = value is null ? null : CodeDecodeValues.Code("PartnerApplicationApprovalStatuses", value); }
  public int CodeDecodePartnerApplicationApprovalStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("PartnerApplicationApprovalStatuses", CodeDecodePartnerApplicationApprovalStatus); set => CodeDecodePartnerApplicationApprovalStatus = CodeDecodeValues.Code("PartnerApplicationApprovalStatuses", value); }
  public string ChangedBy { get; set; } = string.Empty;
  public string? Remarks { get; set; }
  public DateTimeOffset DateTimeCreated { get; set; }
}
