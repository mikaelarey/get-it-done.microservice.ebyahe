using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/buses"), Authorize(Policy = PartnerPortalAuthorization.ReadBusesPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerBusesController(IPartnerBusRepository buses) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;

  [HttpGet]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalBus>>> List([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken) =>
    Ok(await buses.ListAsync(UserId, query.Page, query.PageSize, cancellationToken));

  [HttpGet("workspaces"), Authorize(Policy = PartnerPortalAuthorization.WriteBusesPolicy)]
  public async Task<ActionResult<IReadOnlyList<PartnerBusWorkspace>>> Workspaces(CancellationToken cancellationToken) =>
    Ok(await buses.WorkspacesAsync(UserId, cancellationToken));

  [HttpPost, Authorize(Policy = PartnerPortalAuthorization.WriteBusesPolicy)]
  public async Task<ActionResult<PartnerPortalBus>> Add(AddPartnerBusRequest request, CancellationToken cancellationToken)
  {
    if (request.PartnerId == Guid.Empty || string.IsNullOrWhiteSpace(request.FleetNumber) || string.IsNullOrWhiteSpace(request.BusClass)
      || request.FleetNumber.Any(char.IsControl) || request.BusClass.Any(char.IsControl)) return BadRequest();
    try
    {
      var bus = await buses.AddAsync(UserId, new(request.PartnerId, request.FleetNumber.Trim().ToUpperInvariant(),
        request.PlateNumber.Trim().ToUpperInvariant(), request.BusClass.Trim(), request.SeatCapacity), cancellationToken);
      return bus is null ? Forbid() : StatusCode(StatusCodes.Status201Created, bus);
    }
    catch (PartnerBusConflictException)
    { return Conflict(new ProblemDetails { Title = "A bus with this plate number or fleet number already exists." }); }
  }
}

public sealed class AddPartnerBusRequest
{
  public Guid PartnerId { get; set; }
  [Required, StringLength(64)] public string FleetNumber { get; set; } = "";
  [Required, StringLength(20), RegularExpression(@"^[A-Za-z0-9]+(?:[ -][A-Za-z0-9]+)*$")]
  public string PlateNumber { get; set; } = "";
  [Required, StringLength(100)] public string BusClass { get; set; } = "";
  [Range(1, 100)] public int SeatCapacity { get; set; }
}
