using System.ComponentModel.DataAnnotations;
using EByahe.Domain.DataModels;

namespace EByahe.Application.Dtos.Requests;

public class AddPartnerRoomRequest : IValidatableObject
{
  public Guid HotelId { get; set; }
  [Required, StringLength(200)] public string Name { get; set; } = "";
  [Required, StringLength(100)] public string RoomType { get; set; } = "";
  [Required, Range(1, 100)] public int? GuestCapacity { get; set; }
  [Required, Range(typeof(decimal), "0", "99999999.99")] public decimal? NightlyRate { get; set; }
  [StringLength(2000)] public string? Description { get; set; }
  public IEnumerable<ValidationResult> Validate(ValidationContext context)
  {
    if (HotelId == Guid.Empty) yield return new("Select a hotel.", [nameof(HotelId)]);
    if (Name.Any(char.IsControl) || RoomType.Any(char.IsControl)) yield return new("Room name and type must not contain control characters.", [nameof(Name), nameof(RoomType)]);
    if (NightlyRate is { } rate && decimal.Round(rate, 2) != rate) yield return new("Nightly rate can have up to two decimal places.", [nameof(NightlyRate)]);
  }
  public SavePartnerRoom ToCommand() => new(HotelId, Name.Trim(), RoomType.Trim(), GuestCapacity!.Value, NightlyRate!.Value,
    string.IsNullOrWhiteSpace(Description) ? null : Description.Trim());
}
public sealed class UpdatePartnerRoomRequest : AddPartnerRoomRequest
{
  [Required] public bool? IsActive { get; set; }
}
public class AddPartnerRoomImagesRequest : AddPartnerRoomRequest
{
  [MaxLength(8)] public List<IFormFile> Images { get; set; } = [];
  [MaxLength(8)] public List<Guid> RetainedImageIds { get; set; } = [];
}
public sealed class UpdatePartnerRoomImagesRequest : AddPartnerRoomImagesRequest
{
  [Required] public bool? IsActive { get; set; }
}
