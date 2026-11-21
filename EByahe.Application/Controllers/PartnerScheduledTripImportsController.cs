using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Application.Services;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/scheduled-trips"), Authorize(Policy = PartnerPortalAuthorization.WriteScheduledTripsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerScheduledTripImportsController(IPartnerScheduledTripRepository trips, IUploadRepository uploads, TimeProvider clock) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;

  [HttpGet("template")]
  public async Task<IActionResult> Template([FromQuery] Guid partnerId, [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken ct)
  {
    if (partnerId == Guid.Empty || startDate < ScheduledTripExcelImport.ManilaDate(clock.GetUtcNow()) || endDate < startDate || endDate.DayNumber - startDate.DayNumber >= 31)
      return BadRequest(new ProblemDetails { Title = "Choose today or a future date in Manila time, with a range of 1 to 31 days." });
    var options = await trips.OptionsAsync(UserId, ct);
    if (!options.Workspaces.Any(workspace => workspace.Id == partnerId)) return Forbid();
    if (!options.Buses.Any(bus => bus.PartnerId == partnerId) || options.Terminals.Count(terminal => terminal.PartnerId == partnerId) < 2)
      return BadRequest(new ProblemDetails { Title = "Add at least one active bus and two active terminals before downloading the template." });
    return File(ScheduledTripExcelTemplate.Generate(options, partnerId, startDate, endDate), BusExcelTemplate.ContentType, ScheduledTripExcelTemplate.FileName);
  }

  [HttpPost("import"), RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
  public async Task<ActionResult<PartnerScheduledTripImportResult>> Import([FromForm] ImportScheduledTripsRequest request, CancellationToken ct)
  {
    if (request.PartnerId == Guid.Empty) return BadRequest(new PartnerScheduledTripImportResult(0, [new("", 0, "Partner", "Select a partner.")]));
    var options = await trips.OptionsAsync(UserId, ct);
    if (!options.Workspaces.Any(workspace => workspace.Id == request.PartnerId)) return Forbid();
    var uploadId = await uploads.AddAsync(UserId, UploadTypeCodes.ScheduledTrips, ct);
    var parsed = await ScheduledTripExcelImport.ReadAsync(request.File, request.PartnerId, clock.GetUtcNow(), ct, options);
    if (parsed.Errors.Count != 0) return BadRequest(new PartnerScheduledTripImportResult(0, parsed.Errors, uploadId));
    var result = await trips.ImportAsync(UserId, request.PartnerId, parsed.Rows, uploadId, ct);
    return result is null ? Forbid() : result.Errors.Count != 0 ? BadRequest(result) : Ok(result);
  }
}

public sealed class ImportScheduledTripsRequest
{
  public Guid PartnerId { get; set; }
  [Required] public IFormFile File { get; set; } = null!;
}
