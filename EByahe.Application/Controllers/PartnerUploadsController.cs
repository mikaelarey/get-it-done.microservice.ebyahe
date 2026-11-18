using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/uploads"), Authorize(Policy = PartnerPortalAuthorization.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerUploadsController(IUploadRepository uploads) : ControllerBase
{
  [HttpGet("{id:guid}")]
  public async Task<ActionResult<UploadDetails>> Details(Guid id, [FromQuery] PartnerRecordsQuery query, CancellationToken ct)
  {
    var allowed = new List<int>();
    if (PartnerPortalAuthorization.CanReadBuses(User)) allowed.Add(UploadTypeCodes.Buses);
    if (PartnerPortalAuthorization.CanReadTerminals(User)) allowed.Add(UploadTypeCodes.Terminals);
    if (PartnerPortalAuthorization.CanReadScheduledTrips(User)) allowed.Add(UploadTypeCodes.ScheduledTrips);
    if (PartnerPortalAuthorization.CanReadMembers(User)) allowed.Add(UploadTypeCodes.Users);
    if (allowed.Count == 0) return Forbid();
    var result = await uploads.DetailsAsync(User.FindFirst("sub")!.Value, id, allowed, query.Page, query.PageSize, ct);
    return result is null ? NotFound() : Ok(result);
  }
}
