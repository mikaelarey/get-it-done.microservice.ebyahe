namespace EByahe.Domain.Models;

public sealed class PartnerApplication
{
  public Guid Id { get; set; }
  public string TrackingId { get; set; } = string.Empty;
  public string Status { get; set; } = "Submitted";
  // Null only for applications received before authenticated submissions were introduced.
  public string? CreatedBy { get; set; }
  public string BusinessCategory { get; set; } = string.Empty;
  public string BusinessName { get; set; } = string.Empty;
  public string TradeName { get; set; } = string.Empty;
  public string EntityType { get; set; } = string.Empty;
  public string Tin { get; set; } = string.Empty;
  public string Address { get; set; } = string.Empty;
  public string RepresentativeName { get; set; } = string.Empty;
  public string RepresentativeEmail { get; set; } = string.Empty;
  public string RepresentativeMobile { get; set; } = string.Empty;
  public string BankName { get; set; } = string.Empty;
  public string AccountNumber { get; set; } = string.Empty;
  public string AccountHolder { get; set; } = string.Empty;
  public string SettlementFrequency { get; set; } = string.Empty;
  public DateTimeOffset SubmittedAtUtc { get; set; }
  public ICollection<PartnerApplicationDocument> Documents { get; set; } = new List<PartnerApplicationDocument>();
}
