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
  public int CodeDecodeBusinessCategory { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string BusinessCategory { get => CodeDecodeValues.Decode("BusinessCategories", CodeDecodeBusinessCategory); set => CodeDecodeBusinessCategory = CodeDecodeValues.Code("BusinessCategories", value); }
  public string? RequestedRolesJson { get; set; }
  public string RequestHash { get; set; } = "";
  public int CodeDecodePartnerMemberRequestStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("PartnerMemberRequestStatuses", CodeDecodePartnerMemberRequestStatus); set => CodeDecodePartnerMemberRequestStatus = CodeDecodeValues.Code("PartnerMemberRequestStatuses", value); }
  public Guid? RefUserId { get; set; }
  public DateTimeOffset CreatedAtUtc { get; set; }
  public DateTimeOffset? CompletedAtUtc { get; set; }
}
