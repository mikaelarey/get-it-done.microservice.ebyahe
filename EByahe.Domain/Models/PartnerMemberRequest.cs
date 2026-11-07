namespace EByahe.Domain.Models;

public sealed class PartnerMemberRequest
{
  public Guid Id { get; set; }
  public Guid PartnerId { get; set; }
  public Partner Partner { get; set; } = null!;
  public string CreatedBy { get; set; } = "";
  public string FirstName { get; set; } = "";
  public string LastName { get; set; } = "";
  public string Email { get; set; } = "";
  public string BusinessCategory { get; set; } = "";
  public string RequestHash { get; set; } = "";
  public string Status { get; set; } = "Pending";
  public Guid? RefUserId { get; set; }
  public DateTimeOffset CreatedAtUtc { get; set; }
  public DateTimeOffset? CompletedAtUtc { get; set; }
}
