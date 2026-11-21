using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Dtos.Requests;

public sealed class UpdatePartnerHotelRequest : AddPartnerHotelRequest
{
  [Required] public bool? IsActive { get; set; }
}
