using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController]
[Authorize(Policy = PartnerPortalAuthorization.Policy)]
[Route("api/partner")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerPortalController(IPartnerPortalRepository portal) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;

  [HttpGet("dashboard")]
  public async Task<ActionResult<PartnerDashboard>> Dashboard(CancellationToken cancellationToken) =>
    Ok(await portal.DashboardAsync(UserId, cancellationToken));

  [HttpGet("bookings")]
  public async Task<ActionResult<PartnerRecordsPage<AdminBusBookingSummary>>> Bookings([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken)
  {
    var result = await portal.BookingsAsync(UserId, query.Page, query.PageSize, cancellationToken);
    return Ok(new PartnerRecordsPage<AdminBusBookingSummary>(result.Items.Select(AdminBusBookingSummary.From).ToArray(), result.TotalCount, result.Page, result.PageSize));
  }

  [HttpGet("terminals")]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalTerminal>>> Terminals([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken) =>
    Ok(await portal.TerminalsAsync(UserId, query.Page, query.PageSize, cancellationToken));

  [HttpGet("scheduled-trips")]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalTrip>>> Trips([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken) =>
    Ok(await portal.TripsAsync(UserId, query.Page, query.PageSize, cancellationToken));

  [HttpGet("users")]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalUser>>> Users([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken) =>
    Ok(await portal.UsersAsync(UserId, query.Page, query.PageSize, cancellationToken));
}
