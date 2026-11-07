using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/scheduled-trips"), Authorize(Policy = PartnerPortalAuthorization.WriteBusesPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerScheduledTripsController(IPartnerScheduledTripRepository trips) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;
  [HttpGet("options")]
  public async Task<ActionResult<PartnerTripOptions>> Options(CancellationToken cancellationToken) =>
    Ok(await trips.OptionsAsync(UserId, cancellationToken));

  [HttpPost]
  public async Task<ActionResult<PartnerPortalTrip>> Add(AddPartnerScheduledTripRequest request, CancellationToken cancellationToken)
  {
    if (request.PartnerId == Guid.Empty || request.BusId == Guid.Empty || request.OriginTerminalId == Guid.Empty
      || request.DestinationTerminalId == Guid.Empty || request.OriginTerminalId == request.DestinationTerminalId
      || request.DepartureAtUtc > DateTimeOffset.MaxValue.AddMinutes(-request.DurationMinutes)
      || decimal.Round(request.BaseFare, 2) != request.BaseFare) return BadRequest();
    try
    {
      var trip = await trips.AddAsync(UserId, new(request.PartnerId, request.BusId, request.OriginTerminalId, request.DestinationTerminalId,
        request.DepartureAtUtc, request.DurationMinutes, request.BaseFare), cancellationToken);
      return trip is null ? Forbid() : StatusCode(StatusCodes.Status201Created, trip);
    }
    catch (PartnerScheduledTripConflictException)
    { return Conflict(new ProblemDetails { Title = "This bus already has a scheduled trip during the selected time." }); }
    catch (PartnerScheduledTripValidationException error)
    { return BadRequest(new ProblemDetails { Title = error.Message }); }
  }
}

public sealed class AddPartnerScheduledTripRequest
{
  public Guid PartnerId { get; set; }
  public Guid BusId { get; set; }
  public Guid OriginTerminalId { get; set; }
  public Guid DestinationTerminalId { get; set; }
  public DateTimeOffset DepartureAtUtc { get; set; }
  [Range(1, 2880)] public int DurationMinutes { get; set; }
  [Range(typeof(decimal), "0.01", "1000000")] public decimal BaseFare { get; set; }
}
