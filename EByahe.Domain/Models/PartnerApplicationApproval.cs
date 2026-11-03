namespace EByahe.Domain.Models;

/** One immutable entry per approval status transition. The latest entry is the current approval state. */
public sealed class PartnerApplicationApproval
{
  public Guid Id { get; set; }
  public Guid PartnerApplicationId { get; set; }
  public string? PreviousStatus { get; set; }
  public string Status { get; set; } = "Pending";
  public string ChangedBy { get; set; } = string.Empty;
  public string? Remarks { get; set; }
  public DateTimeOffset DateTimeCreated { get; set; }
}
