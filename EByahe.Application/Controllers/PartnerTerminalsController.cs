using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using EByahe.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using EByahe.Domain.Models;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/terminals"), Authorize(Policy = PartnerPortalAuthorization.WriteTerminalsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerTerminalsController(IPartnerTerminalRepository terminals, IUploadRepository uploads) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<PartnerTerminalForUpdate>> GetForUpdate(Guid id, CancellationToken cancellationToken) =>
    await terminals.GetForUpdateAsync(UserId, id, cancellationToken) is { } terminal ? Ok(terminal) : NotFound();

  [HttpPut("{id:guid}")]
  public async Task<ActionResult<PartnerPortalTerminal>> Update(Guid id, UpdatePartnerTerminalRequest request, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address)
      || request.Name.Any(char.IsControl)
      || request.Address.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t')) return BadRequest();
    var codes = new[] { request.CodeDecodeRegion, request.CodeDecodeProvince, request.CodeDecodeMunicipality, request.CodeDecodeBarangay };
    if (codes.Any(code => code.HasValue) && codes.Any(code => !code.HasValue)) return BadRequest();
    try
    {
      var terminal = await terminals.UpdateAsync(UserId, id, new(request.Name.Trim(), request.Address.Trim(), request.IsActive!.Value,
        request.CodeDecodeRegion, request.CodeDecodeProvince, request.CodeDecodeMunicipality, request.CodeDecodeBarangay), cancellationToken);
      return terminal is null ? NotFound() : Ok(terminal);
    }
    catch (PartnerTerminalConflictException)
    { return Conflict(new ProblemDetails { Title = "A terminal with this name already exists at this location for your partner." }); }
    catch (PartnerTerminalLocationUnavailableException)
    { return BadRequest(new ProblemDetails { Title = "Select a valid region, province, city/municipality and barangay. The location must be active." }); }
  }

  [HttpGet("workspaces")]
  public async Task<ActionResult<IReadOnlyList<PartnerTerminalWorkspace>>> Workspaces(CancellationToken cancellationToken) =>
    Ok(await terminals.WorkspacesAsync(UserId, cancellationToken));

  [HttpGet("locations")]
  public async Task<ActionResult<IReadOnlyList<PartnerTerminalLocation>>> Locations(CancellationToken cancellationToken) =>
    Ok(await terminals.LocationsAsync(UserId, cancellationToken));

  [HttpGet("regions")]
  public async Task<ActionResult<IReadOnlyList<GeographyOption>>> Regions(CancellationToken cancellationToken) =>
    Ok(await terminals.RegionsAsync(UserId, cancellationToken));

  [HttpGet("provinces")]
  public async Task<ActionResult<IReadOnlyList<GeographyOption>>> Provinces([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeRegion, CancellationToken cancellationToken) =>
    Ok(await terminals.ProvincesAsync(UserId, codeDecodeRegion, cancellationToken));

  [HttpGet("municipalities")]
  public async Task<ActionResult<IReadOnlyList<GeographyOption>>> Municipalities([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeProvince, CancellationToken cancellationToken) =>
    Ok(await terminals.MunicipalitiesAsync(UserId, codeDecodeProvince, cancellationToken));

  [HttpGet("barangays")]
  public async Task<ActionResult<IReadOnlyList<GeographyOption>>> Barangays([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeMunicipality, CancellationToken cancellationToken) =>
    Ok(await terminals.BarangaysAsync(UserId, codeDecodeMunicipality, cancellationToken));

  [HttpPost]
  public async Task<ActionResult<PartnerPortalTerminal>> Add(AddPartnerTerminalRequest request, CancellationToken cancellationToken)
  {
    if (request.PartnerId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)
      || string.IsNullOrWhiteSpace(request.Address) || request.Name.Any(char.IsControl)
      || request.Address.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t')) return BadRequest();
    try
    {
      var terminal = await terminals.AddAsync(UserId, new(request.PartnerId, request.CodeDecodeRegion, request.CodeDecodeProvince,
        request.CodeDecodeMunicipality, request.CodeDecodeBarangay, request.Name.Trim(), request.Address.Trim()), cancellationToken);
      return terminal is null ? Forbid() : StatusCode(StatusCodes.Status201Created, terminal);
    }
    catch (PartnerTerminalConflictException)
    { return Conflict(new ProblemDetails { Title = "A terminal with this name already exists at this location for your partner." }); }
    catch (PartnerTerminalLocationUnavailableException)
    { return BadRequest(new ProblemDetails { Title = "Select a valid region, province, city/municipality and barangay. The location must be active." }); }
  }

  [HttpPost("import")]
  [RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
  public async Task<ActionResult<PartnerTerminalImportResult>> Import([FromForm] ImportPartnerTerminalsRequest request, CancellationToken cancellationToken)
  {
    if (request.PartnerId == Guid.Empty) return BadRequest(new PartnerTerminalImportResult(0, [new(0, "Partner", "Select a partner.")]));
    if (!(await terminals.WorkspacesAsync(UserId, cancellationToken)).Any(workspace => workspace.Id == request.PartnerId)) return Forbid();
    var uploadId = await uploads.AddAsync(UserId, UploadTypeCodes.Terminals, cancellationToken);
    var parsed = await TerminalExcelImport.ReadAsync(request.File, request.PartnerId, cancellationToken);
    if (parsed.Errors.Count != 0) return BadRequest(new PartnerTerminalImportResult(0, parsed.Errors));
    try
    {
      var result = await terminals.ImportAsync(UserId, request.PartnerId, parsed.Rows, uploadId, cancellationToken);
      return result is null ? Forbid() : result.Errors.Any(error => error.Field is not "Terminal name") ? BadRequest(result)
        : result.Errors.Count != 0 ? Conflict(result) : Ok(result);
    }
    catch (PartnerTerminalConflictException)
    { return Conflict(new PartnerTerminalImportResult(0, [new(0, "File", "A terminal now exists at this location. Refresh the list and correct the file before importing again.")])); }
  }
}

public sealed class ImportPartnerTerminalsRequest
{
  public Guid PartnerId { get; set; }
  [Required] public IFormFile File { get; set; } = null!;
}

public sealed class AddPartnerTerminalRequest
{
  public Guid PartnerId { get; set; }
  [Range(1, int.MaxValue)] public int CodeDecodeRegion { get; set; }
  [Range(1, int.MaxValue)] public int CodeDecodeProvince { get; set; }
  [Range(1, int.MaxValue)] public int CodeDecodeMunicipality { get; set; }
  [Range(1, int.MaxValue)] public int CodeDecodeBarangay { get; set; }
  [Required, StringLength(254)] public string Name { get; set; } = "";
  [Required, StringLength(500)] public string Address { get; set; } = "";
}

public sealed class UpdatePartnerTerminalRequest
{
  [Range(1, int.MaxValue)] public int? CodeDecodeRegion { get; set; }
  [Range(1, int.MaxValue)] public int? CodeDecodeProvince { get; set; }
  [Range(1, int.MaxValue)] public int? CodeDecodeMunicipality { get; set; }
  [Range(1, int.MaxValue)] public int? CodeDecodeBarangay { get; set; }
  [Required, StringLength(254)] public string Name { get; set; } = "";
  [Required, StringLength(500)] public string Address { get; set; } = "";
  [Required] public bool? IsActive { get; set; }
}
