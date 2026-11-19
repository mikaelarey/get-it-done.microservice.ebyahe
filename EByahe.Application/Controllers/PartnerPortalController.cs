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
public sealed class PartnerPortalController(IPartnerPortalRepository portal, TimeProvider clock) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;

  [HttpGet("dashboard")]
  public async Task<ActionResult<PartnerDashboard>> Dashboard(CancellationToken cancellationToken) =>
    Ok(await portal.DashboardAsync(UserId, cancellationToken));

  [HttpGet("dashboard/overview")]
  public async Task<ActionResult<PartnerDashboardOverview>> DashboardOverview(CancellationToken cancellationToken, [FromQuery] string period = "week")
  {
    if (period is not ("week" or "month")) return BadRequest(new ProblemDetails { Title = "Select week or month." });
    var access = new PartnerDashboardAccess(PartnerPortalAuthorization.CanReadBookings(User),
      PartnerPortalAuthorization.CanReadScheduledTrips(User), PartnerPortalAuthorization.CanReadTerminals(User),
      PartnerPortalAuthorization.CanReadMembers(User), PartnerPortalAuthorization.CanReadBuses(User));
    return Ok(await portal.DashboardOverviewAsync(UserId, period, clock.GetUtcNow(), access, cancellationToken));
  }

  [HttpGet("bookings"), Authorize(Policy = PartnerPortalAuthorization.ReadBookingsPolicy)]
  public async Task<ActionResult<PartnerRecordsPage<AdminBusBookingSummary>>> Bookings([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken)
  {
    var result = await portal.BookingsAsync(UserId, query.Page, query.PageSize, cancellationToken, query.Search);
    return Ok(new PartnerRecordsPage<AdminBusBookingSummary>(result.Items.Select(AdminBusBookingSummary.From).ToArray(), result.TotalCount, result.Page, result.PageSize));
  }

  [HttpGet("bookings/{id:guid}"), Authorize(Policy = PartnerPortalAuthorization.ReadBookingsPolicy)]
  public async Task<ActionResult<BusBookingDetailsResponse>> BookingDetails(Guid id, CancellationToken cancellationToken)
  {
    var booking = await portal.BookingDetailsAsync(UserId, id, cancellationToken);
    return booking is null ? NotFound() : Ok(BusBookingDetailsResponse.From(booking));
  }

  [HttpGet("terminals"), Authorize(Policy = PartnerPortalAuthorization.ReadTerminalsPolicy)]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalTerminal>>> Terminals([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken) =>
    Ok(await portal.TerminalsAsync(UserId, query.Page, query.PageSize, cancellationToken, query.Search));

  [HttpGet("scheduled-trips"), Authorize(Policy = PartnerPortalAuthorization.ReadScheduledTripsPolicy)]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalTrip>>> Trips([FromQuery] PartnerRecordsQuery query,
    [FromQuery] DateOnly? date, [FromQuery] Guid? terminalId, CancellationToken cancellationToken)
  {
    if (date == DateOnly.MinValue || terminalId == Guid.Empty) return BadRequest();
    var selectedDate = date ?? DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(8)).DateTime);
    return Ok(await portal.TripsAsync(UserId, query.Page, query.PageSize, selectedDate, terminalId, cancellationToken));
  }

  [HttpGet("scheduled-trips/filter-terminals"), Authorize(Policy = PartnerPortalAuthorization.ReadScheduledTripsPolicy)]
  public async Task<ActionResult<IReadOnlyList<PartnerTripFilterTerminal>>> TripFilterTerminals(CancellationToken cancellationToken) =>
    Ok(await portal.TripFilterTerminalsAsync(UserId, cancellationToken));

  [HttpGet("users"), Authorize(Policy = PartnerPortalAuthorization.ReadMembersPolicy)]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalUser>>> Users([FromQuery] PartnerRecordsQuery query, CancellationToken cancellationToken) =>
    Ok(await portal.UsersAsync(UserId, query.Page, query.PageSize, PartnerPortalAuthorization.CanCreateMembers(User), cancellationToken, query.Search));

  [HttpGet("users/{id:guid}"), Authorize(Policy = PartnerPortalAuthorization.ReadMembersPolicy)]
  public async Task<ActionResult<PartnerPortalUserDetails>> UserDetails(Guid id, CancellationToken cancellationToken)
  {
    var result = await portal.UserDetailsAsync(UserId, id, cancellationToken);
    return result is null ? NotFound() : Ok(result);
  }
}
