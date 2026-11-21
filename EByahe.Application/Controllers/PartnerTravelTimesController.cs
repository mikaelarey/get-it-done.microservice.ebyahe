using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/travel-times"), Authorize(Policy = PartnerPortalAuthorization.ReadTravelTimesPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerTravelTimesController(IPartnerScheduledTripRepository trips) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;
  [HttpGet]
  public async Task<ActionResult<PartnerRecordsPage<PartnerTravelTime>>> List([FromQuery] PartnerRecordsQuery query, CancellationToken ct,
    [FromQuery] Guid? originTerminalId, [FromQuery] Guid? destinationTerminalId)
  {
    if (originTerminalId == Guid.Empty || destinationTerminalId == Guid.Empty) return BadRequest();
    return Ok(await trips.TravelTimesAsync(UserId, query.Page, query.PageSize, query.Search, ct, originTerminalId, destinationTerminalId));
  }
  [HttpGet("filter-terminals")]
  public async Task<ActionResult<IReadOnlyList<PartnerTripFilterTerminal>>> FilterTerminals(CancellationToken ct) =>
    Ok(await trips.TravelTimeFilterTerminalsAsync(UserId, ct));
  [HttpGet("options")]
  public async Task<ActionResult<PartnerTravelTimeOptions>> Options(CancellationToken ct) => Ok(await trips.TravelTimeOptionsAsync(UserId, ct));

  [HttpPost, Authorize(Policy = PartnerPortalAuthorization.WriteTravelTimesPolicy)]
  public async Task<ActionResult<PartnerTravelTime>> Add(AddPartnerTravelTimeRequest request, CancellationToken ct)
  {
    if (request.PartnerId == Guid.Empty || request.OriginTerminalId == Guid.Empty || request.DestinationTerminalId == Guid.Empty) return BadRequest();
    try {
      var route = await trips.AddTravelTimeAsync(UserId, new(request.PartnerId, request.OriginTerminalId, request.DestinationTerminalId, request.DurationMinutes), ct);
      return route is null ? Forbid() : StatusCode(201, route);
    } catch (PartnerScheduledTripValidationException error) { return BadRequest(new ProblemDetails { Title = error.Message }); }
      catch (PartnerScheduledTripConflictException error) { return Conflict(new ProblemDetails { Title = error.Message }); }
  }

  [HttpPut("{id:guid}"), Authorize(Policy = PartnerPortalAuthorization.WriteTravelTimesPolicy)]
  public async Task<ActionResult<PartnerTravelTime>> Update(Guid id, UpdatePartnerTravelTimeRequest request, CancellationToken ct)
  {
    try {
      var route = await trips.UpdateTravelTimeAsync(UserId, id, new(request.DurationMinutes, request.IsActive!.Value), ct);
      return route is null ? NotFound() : Ok(route);
    } catch (PartnerScheduledTripValidationException error) { return BadRequest(new ProblemDetails { Title = error.Message }); }
      catch (PartnerScheduledTripConflictException error) { return Conflict(new ProblemDetails { Title = error.Message }); }
  }
}
public sealed class AddPartnerTravelTimeRequest
{
  public Guid PartnerId { get; set; }
  public Guid OriginTerminalId { get; set; }
  public Guid DestinationTerminalId { get; set; }
  [Range(1, 2880)] public int DurationMinutes { get; set; }
}
public sealed class UpdatePartnerTravelTimeRequest
{
  [Range(1, 2880)] public int DurationMinutes { get; set; }
  [Required] public bool? IsActive { get; set; }
}
