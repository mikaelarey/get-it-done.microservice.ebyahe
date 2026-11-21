using System.ComponentModel.DataAnnotations;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Dtos.Requests;

public class AddPartnerHotelRequest : IValidatableObject
{
  public Guid PartnerId { get; set; }
  [Required, StringLength(200)] public string Name { get; set; } = "";
  [Required, StringLength(1000)] public string Address { get; set; } = "";
  [Range(1, int.MaxValue)] public int CodeDecodeRegion { get; set; }
  [Range(1, int.MaxValue)] public int CodeDecodeProvince { get; set; }
  [Range(1, int.MaxValue)] public int CodeDecodeMunicipality { get; set; }
  [Range(1, int.MaxValue)] public int CodeDecodeBarangay { get; set; }
  [Required, StringLength(50), Phone] public string ContactNumber { get; set; } = "";
  [Required, StringLength(254), EmailAddress] public string Email { get; set; } = "";
  [StringLength(2000)] public string? Description { get; set; }

  public IEnumerable<ValidationResult> Validate(ValidationContext context)
  {
    if (PartnerId == Guid.Empty) yield return new("Select a hotel company.", [nameof(PartnerId)]);
    if (Name.Any(char.IsControl)) yield return new("Hotel name must not contain control characters.", [nameof(Name)]);
    if (Address.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
      yield return new("Street address contains invalid characters.", [nameof(Address)]);
  }
  public AddPartnerHotel ToCommand() => new(PartnerId, Name.Trim(), Address.Trim(), CodeDecodeRegion, CodeDecodeProvince,
    CodeDecodeMunicipality, CodeDecodeBarangay, ContactNumber.Trim(), Email.Trim(), string.IsNullOrWhiteSpace(Description) ? null : Description.Trim());
}
