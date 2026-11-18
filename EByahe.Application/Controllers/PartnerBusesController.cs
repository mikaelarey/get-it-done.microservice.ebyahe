using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EByahe.Application.Services;

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

  [HttpGet("classes")]
  public async Task<ActionResult<IReadOnlyList<BusClassOption>>> Classes(CancellationToken cancellationToken) =>
    Ok(await buses.ClassesAsync(cancellationToken));

  [HttpGet("template"), Authorize(Policy = PartnerPortalAuthorization.WriteBusesPolicy)]
  public async Task<IActionResult> Template(CancellationToken cancellationToken)
  {
    var classes = await buses.ClassesAsync(cancellationToken);
    if (classes.Count == 0) return Problem(title: "Bus classes are not configured. Please try again later.", statusCode: StatusCodes.Status503ServiceUnavailable);
    return File(BusExcelTemplate.Generate(classes), BusExcelTemplate.ContentType, BusExcelTemplate.FileName);
  }

  [HttpPost, Authorize(Policy = PartnerPortalAuthorization.WriteBusesPolicy)]
  public async Task<ActionResult<PartnerPortalBus>> Add(AddPartnerBusRequest request, CancellationToken cancellationToken)
  {
    try
    {
      var bus = await buses.AddAsync(UserId, request.ToCommand(), cancellationToken);
      return bus is null ? Forbid() : StatusCode(StatusCodes.Status201Created, bus);
    }
    catch (PartnerBusConflictException)
    { return Conflict(new ProblemDetails { Title = "A bus with this plate number or fleet number already exists." }); }
    catch (PartnerBusClassValidationException)
    { return BadRequest(new ProblemDetails { Title = "Select a registered bus class." }); }
  }

  [HttpPost("import"), Authorize(Policy = PartnerPortalAuthorization.WriteBusesPolicy)]
  [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
  public async Task<ActionResult<PartnerBusImportResult>> Import([FromForm] ImportPartnerBusesRequest request, CancellationToken cancellationToken)
  {
    if (request.PartnerId == Guid.Empty) return BadRequest(new PartnerBusImportResult(0, [new(0, "Partner", "Select a partner.")]));
    if (!(await buses.WorkspacesAsync(UserId, cancellationToken)).Any(workspace => workspace.Id == request.PartnerId)) return Forbid();
    var parsed = await BusExcelImport.ReadAsync(request.File, request.PartnerId, cancellationToken);
    if (parsed.Errors.Count != 0) return BadRequest(new PartnerBusImportResult(0, parsed.Errors));
    try
    {
      var result = await buses.ImportAsync(UserId, request.PartnerId, parsed.Rows, cancellationToken);
      return result is null ? Forbid() : result.Errors.Any(error => error.Field == "Bus class") ? BadRequest(result)
        : result.Errors.Count != 0 ? Conflict(result) : Ok(result);
    }
    catch (PartnerBusConflictException)
    { return Conflict(new PartnerBusImportResult(0, [new(0, "File", "A fleet number or plate number now exists. Refresh your fleet and correct the file before importing again.")])); }
  }
}

public sealed class ImportPartnerBusesRequest
{
  public Guid PartnerId { get; set; }
  [Required] public IFormFile File { get; set; } = null!;
}
