using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner/hotels"), Authorize(Policy = PartnerPortalAuthorization.ReadHotelsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerHotelsController(IPartnerHotelRepository hotels, IPartnerApplicationRepository applications) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;

  [HttpDelete("{id:guid}"), Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
    await hotels.DeleteAsync(UserId, id, ct) ? NoContent() : NotFound();
  [HttpGet]
  public async Task<ActionResult<PartnerRecordsPage<PartnerHotel>>> List([FromQuery] PartnerRecordsQuery query, CancellationToken ct) =>
    Ok(await hotels.ListAsync(UserId, query.Page, query.PageSize, query.Search, ct));

  [HttpGet("{id:guid}")]
  public async Task<ActionResult<PartnerHotel>> Get(Guid id, CancellationToken ct) =>
    await hotels.GetAsync(UserId, id, ct) is { } hotel ? Ok(hotel) : NotFound();

  [HttpPut("{id:guid}"), Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public async Task<ActionResult<PartnerHotel>> Update(Guid id, UpdatePartnerHotelRequest request, CancellationToken ct)
  {
    try
    {
      var hotel = await hotels.UpdateAsync(UserId, id, request.ToCommand(), request.IsActive!.Value, ct);
      return hotel is null ? NotFound() : Ok(hotel);
    }
    catch (PartnerHotelAddressException)
    { return BadRequest(new ProblemDetails { Title = "Select a valid region, province, city/municipality and barangay from the same address hierarchy." }); }
    catch (PartnerHotelConflictException)
    { return Conflict(new ProblemDetails { Title = "A hotel with this name already exists in this barangay for your company." }); }
  }

  [HttpGet("workspaces"), Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public async Task<ActionResult<IReadOnlyList<PartnerHotelWorkspace>>> Workspaces(CancellationToken ct) =>
    Ok(await hotels.WorkspacesAsync(UserId, ct));

  [HttpGet("regions"), Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public Task<IReadOnlyList<GeographyOption>> Regions(CancellationToken ct) => applications.GeographyAsync("regions", null, ct);
  [HttpGet("provinces"), Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public Task<IReadOnlyList<GeographyOption>> Provinces([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeRegion, CancellationToken ct) =>
    applications.GeographyAsync("provinces", codeDecodeRegion, ct);
  [HttpGet("municipalities"), Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public Task<IReadOnlyList<GeographyOption>> Municipalities([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeProvince, CancellationToken ct) =>
    applications.GeographyAsync("municipalities", codeDecodeProvince, ct);
  [HttpGet("barangays"), Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public Task<IReadOnlyList<GeographyOption>> Barangays([FromQuery, BindRequired, Range(1, int.MaxValue)] int codeDecodeMunicipality, CancellationToken ct) =>
    applications.GeographyAsync("barangays", codeDecodeMunicipality, ct);

  [HttpPost, Authorize(Policy = PartnerPortalAuthorization.WriteHotelsPolicy)]
  public async Task<ActionResult<PartnerHotel>> Add(AddPartnerHotelRequest request, CancellationToken ct)
  {
    try
    {
      var hotel = await hotels.AddAsync(UserId, request.ToCommand(), ct);
      return hotel is null ? Forbid() : StatusCode(StatusCodes.Status201Created, hotel);
    }
    catch (PartnerHotelAddressException)
    { return BadRequest(new ProblemDetails { Title = "Select a valid region, province, city/municipality and barangay from the same address hierarchy." }); }
    catch (PartnerHotelConflictException)
    { return Conflict(new ProblemDetails { Title = "A hotel with this name already exists in this barangay for your company." }); }
  }
}
