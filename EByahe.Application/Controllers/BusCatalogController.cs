using EByahe.Application.Validation;
using System.ComponentModel.DataAnnotations;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, AllowAnonymous, Route("api/bus-catalog")]
public sealed class BusCatalogController(IBusCatalogRepository catalog) : ControllerBase
{
  [HttpGet("locations")]
  public async Task<ActionResult<IReadOnlyList<BusLocationResponse>>> Locations(CancellationToken ct) =>
    Ok((await catalog.LocationsAsync(null, ct)).Select(x => new BusLocationResponse(x.Id, x.Name)));

  [HttpGet("destinations")]
  public async Task<ActionResult<IReadOnlyList<BusLocationResponse>>> Destinations([FromQuery, NonEmptyGuid] Guid originId, CancellationToken ct) =>
    Ok((await catalog.LocationsAsync(originId, ct)).Select(x => new BusLocationResponse(x.Id, x.Name)));

  [HttpGet("trips")]
  public async Task<ActionResult<IReadOnlyList<BusTripResponse>>> Search(
    [FromQuery, NonEmptyGuid] Guid originId, [FromQuery, NonEmptyGuid] Guid destinationId,
    [FromQuery, Required] DateOnly? departure, [FromQuery, Range(1, 20)] int passengers = 1, CancellationToken ct = default)
  {
    if (originId == destinationId) return BadRequest(new ProblemDetails { Title = "Origin and destination must differ." });
    return Ok((await catalog.SearchAsync(originId, destinationId, departure!.Value, passengers, ct)).Select(BusTripResponse.From));
  }

  [HttpGet("trips/{id:guid}")]
  public async Task<ActionResult<BusTripResponse>> Trip([NonEmptyGuid] Guid id, CancellationToken ct) =>
    await catalog.GetAsync(id, ct) is { } trip ? Ok(BusTripResponse.From(trip)) : NotFound();
}
