using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/terminals"), Authorize(Policy = PartnerPortalAuthorization.WriteBusesPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerTerminalsController(IPartnerTerminalRepository terminals) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;

  [HttpGet("workspaces")]
  public async Task<ActionResult<IReadOnlyList<PartnerTerminalWorkspace>>> Workspaces(CancellationToken cancellationToken) =>
    Ok(await terminals.WorkspacesAsync(UserId, cancellationToken));

  [HttpGet("locations")]
  public async Task<ActionResult<IReadOnlyList<PartnerTerminalLocation>>> Locations(CancellationToken cancellationToken) =>
    Ok(await terminals.LocationsAsync(UserId, cancellationToken));

  [HttpPost]
  public async Task<ActionResult<PartnerPortalTerminal>> Add(AddPartnerTerminalRequest request, CancellationToken cancellationToken)
  {
    if (request.PartnerId == Guid.Empty || request.LocationId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)
      || string.IsNullOrWhiteSpace(request.Address) || request.Name.Any(char.IsControl)
      || request.Address.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t')) return BadRequest();
    try
    {
      var terminal = await terminals.AddAsync(UserId, new(request.PartnerId, request.LocationId, request.Name.Trim(), request.Address.Trim()), cancellationToken);
      return terminal is null ? Forbid() : StatusCode(StatusCodes.Status201Created, terminal);
    }
    catch (PartnerTerminalConflictException)
    { return Conflict(new ProblemDetails { Title = "A terminal with this name already exists at this location for your partner." }); }
    catch (PartnerTerminalLocationUnavailableException)
    { return BadRequest(new ProblemDetails { Title = "Select an active location." }); }
  }
}

public sealed class AddPartnerTerminalRequest
{
  public Guid PartnerId { get; set; }
  public Guid LocationId { get; set; }
  [Required, StringLength(254)] public string Name { get; set; } = "";
  [Required, StringLength(500)] public string Address { get; set; } = "";
}
