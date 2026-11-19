using System.ComponentModel.DataAnnotations;
using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EByahe.Application.Controllers;

// Applicants may use address lookups before they have a partner membership.
[ApiController, Authorize, Route("api/partner-applications")]
public sealed class PartnerApplicationGeographyController(IPartnerApplicationRepository applications) : ControllerBase
{
  [HttpGet("regions")]
  public Task<IReadOnlyList<GeographyOption>> Regions(CancellationToken ct) => applications.GeographyAsync("regions", null, ct);
  [HttpGet("provinces")]
  public Task<IReadOnlyList<GeographyOption>> Provinces([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeRegion, CancellationToken ct) =>
    applications.GeographyAsync("provinces", codeDecodeRegion, ct);
  [HttpGet("municipalities")]
  public Task<IReadOnlyList<GeographyOption>> Municipalities([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeProvince, CancellationToken ct) =>
    applications.GeographyAsync("municipalities", codeDecodeProvince, ct);
  [HttpGet("barangays")]
  public Task<IReadOnlyList<GeographyOption>> Barangays([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeMunicipality, CancellationToken ct) =>
    applications.GeographyAsync("barangays", codeDecodeMunicipality, ct);
}
