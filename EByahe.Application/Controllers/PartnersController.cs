using System.ComponentModel.DataAnnotations;
using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController]
[Authorize(Policy = PartnerApplicationReviewAuthorization.Policy)]
[Route("api/admin/partners")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnersController(IPartnerDirectoryRepository partners) : ControllerBase
{
  [HttpGet("{id:guid}/buses")]
  public async Task<ActionResult<PartnerRecordsPage<PartnerBusRecord>>> Buses(Guid id, [FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken)
  {
    var page = await partners.BusesAsync(id, query.Page, query.PageSize, cancellationToken);
    return page is null ? NotFound() : Ok(page);
  }

  [HttpGet("{id:guid}/terminals")]
  public async Task<ActionResult<PartnerRecordsPage<PartnerTerminalRecord>>> Terminals(Guid id, [FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken)
  {
    var page = await partners.TerminalsAsync(id, query.Page, query.PageSize, cancellationToken);
    return page is null ? NotFound() : Ok(page);
  }

  [HttpGet("{id:guid}/bookings")]
  public async Task<ActionResult<PartnerRecordsPage<PartnerBookingRecord>>> Bookings(Guid id, [FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken)
  {
    var page = await partners.BookingsAsync(id, query.Page, query.PageSize, cancellationToken);
    return page is null ? NotFound() : Ok(page);
  }

  [HttpGet("{id:guid}/users")]
  public async Task<ActionResult<PartnerRecordsPage<PartnerUserRecord>>> Users(Guid id, [FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken)
  {
    var page = await partners.UsersAsync(id, query.Page, query.PageSize, cancellationToken);
    return page is null ? NotFound() : Ok(page);
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType<PartnerDirectoryDetails>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PartnerDirectoryDetails>> Details(Guid id, CancellationToken cancellationToken)
  {
    var partner = await partners.GetAsync(id, cancellationToken);
    return partner is null ? NotFound() : Ok(partner);
  }

  [HttpGet]
  [ProducesResponseType<PartnerDirectoryPage>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<PartnerDirectoryPage>> List(
    [FromQuery, Range(1, 1000000)] int page = 1,
    [FromQuery, Range(1, 50)] int pageSize = 5,
    [FromQuery, StringLength(254)] string? search = null,
    [FromQuery, StringLength(32)] string? category = null,
    [FromQuery, StringLength(32)] string? status = null,
    CancellationToken cancellationToken = default) =>
    Ok(await partners.ListAsync(page, pageSize, search, category, status, cancellationToken));
}
