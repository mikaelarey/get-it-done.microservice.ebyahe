using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Dtos.Requests;

public sealed class PartnerApplicationDecisionRequest
{
  [StringLength(2000)]
  public string? Remarks { get; init; }
}
