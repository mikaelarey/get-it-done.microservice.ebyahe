namespace EByahe.Domain.Models;

public sealed class PartnerApplication
{
  public Guid Id { get; set; }
  public string TrackingId { get; set; } = string.Empty;
  public int CodeDecodePartnerApplicationStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("PartnerApplicationStatuses", CodeDecodePartnerApplicationStatus); set => CodeDecodePartnerApplicationStatus = CodeDecodeValues.Code("PartnerApplicationStatuses", value); }
  // Null only for applications received before authenticated submissions were introduced.
  public string? CreatedBy { get; set; }
  public int CodeDecodeBusinessCategory { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string BusinessCategory { get => CodeDecodeValues.Decode("BusinessCategories", CodeDecodeBusinessCategory); set => CodeDecodeBusinessCategory = CodeDecodeValues.Code("BusinessCategories", value); }
  public string BusinessName { get; set; } = string.Empty;
  public string TradeName { get; set; } = string.Empty;
  public int CodeDecodeEntityType { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string EntityType { get => CodeDecodeValues.Decode("EntityTypes", CodeDecodeEntityType); set => CodeDecodeEntityType = CodeDecodeValues.Code("EntityTypes", value); }
  public string Tin { get; set; } = string.Empty;
  public string Address { get; set; } = string.Empty;
  public string RepresentativeName { get; set; } = string.Empty;
  public string RepresentativeEmail { get; set; } = string.Empty;
  public string RepresentativeMobile { get; set; } = string.Empty;
  public int? CodeDecodeBank { get; set; }
  [System.Text.Json.Serialization.JsonIgnore]
  public CodeDecodeBank? Bank { get; set; }
  private string? pendingBankName;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string BankName { get => pendingBankName ?? Bank?.Decode ?? string.Empty; set => pendingBankName = value; }
  public bool HasPendingBankName() => pendingBankName is not null;
  public void ClearPendingBankName() => pendingBankName = null;
  public string AccountNumber { get; set; } = string.Empty;
  public string AccountHolder { get; set; } = string.Empty;
  public int CodeDecodeSettlementFrequency { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string SettlementFrequency { get => CodeDecodeValues.Decode("SettlementFrequencies", CodeDecodeSettlementFrequency); set => CodeDecodeSettlementFrequency = CodeDecodeValues.Code("SettlementFrequencies", value); }
  public DateTimeOffset SubmittedAtUtc { get; set; }
  public ICollection<PartnerApplicationDocument> Documents { get; set; } = new List<PartnerApplicationDocument>();
}
