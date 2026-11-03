using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Dtos.Requests;

public sealed class CreatePartnerApplicationRequest
{
  [Required, RegularExpression("^(hotel|bus-operator)$")]
  public string BusinessCategory { get; set; } = string.Empty;
  [Required] public PartnerCorporateRequest Corporate { get; set; } = new();
  [Required] public PartnerPayoutRequest Payout { get; set; } = new();
  [Required] public PartnerDocumentsRequest Documents { get; set; } = new();
}

public sealed class PartnerCorporateRequest
{
  [Required, StringLength(255)] public string BusinessName { get; set; } = string.Empty;
  [Required, StringLength(255)] public string TradeName { get; set; } = string.Empty;
  [Required, RegularExpression("^(sole-proprietorship|partnership|corporation)$")]
  public string EntityType { get; set; } = string.Empty;
  [Required, StringLength(32)] public string Tin { get; set; } = string.Empty;
  [Required, StringLength(2000)] public string Address { get; set; } = string.Empty;
  [Required, StringLength(255)] public string RepresentativeName { get; set; } = string.Empty;
  [Required, StringLength(254), EmailAddress] public string RepresentativeEmail { get; set; } = string.Empty;
  [Required, StringLength(32), RegularExpression(@"^\+?(?:[\s().-]*[0-9]){10,15}[\s().-]*$")]
  public string RepresentativeMobile { get; set; } = string.Empty;
}

public sealed class PartnerPayoutRequest
{
  [Required, StringLength(100)] public string BankName { get; set; } = string.Empty;
  [Required, StringLength(64), RegularExpression(@"^(?=.*[0-9])[0-9\s-]+$")]
  public string AccountNumber { get; set; } = string.Empty;
  [Required, StringLength(255)] public string AccountHolder { get; set; } = string.Empty;
  [Required, RegularExpression("^(daily|weekly)$")] public string SettlementFrequency { get; set; } = string.Empty;
}

public sealed class PartnerDocumentsRequest
{
  [Required] public IFormFile? Registration { get; set; }
  [Required] public IFormFile? Bir { get; set; }
  [Required] public IFormFile? Permit { get; set; }
  [Required] public IFormFile? Accreditation { get; set; }
  [Required] public IFormFile? BankAccountProof { get; set; }
}
