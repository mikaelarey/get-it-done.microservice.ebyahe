using System.ComponentModel.DataAnnotations;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Dtos.Requests;

public sealed class UpdatePartnerBusRequest : IValidatableObject
{
  [Required, StringLength(64)] public string FleetNumber { get; set; } = "";
  [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9]+(?:[ -][A-Za-z0-9]+)*$")]
  public string PlateNumber { get; set; } = "";
  [Required, StringLength(100)] public string BusClass { get; set; } = "";
  [Range(1, 100)] public int SeatCapacity { get; set; }
  [Required] public bool? IsActive { get; set; }

  public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
  {
    if (FleetNumber.Any(char.IsControl)) yield return new("Fleet number must not contain control characters.", [nameof(FleetNumber)]);
    if (BusClass.Any(char.IsControl)) yield return new("Bus class must not contain control characters.", [nameof(BusClass)]);
  }
  public UpdatePartnerBus ToCommand() => new(FleetNumber.Trim().ToUpperInvariant(), PlateNumber.Trim().ToUpperInvariant(),
    BusClass.Trim(), SeatCapacity, IsActive!.Value);
}
