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

  [HttpGet("bookings/{id:guid}/cash-payment-summary"), Authorize(Policy = PartnerPortalAuthorization.ConfirmBookingsPolicy)]
  public async Task<ActionResult<PartnerCashBookingSummary>> CashPaymentSummary(Guid id, CancellationToken cancellationToken)
  {
    var booking = await portal.CashPaymentSummaryAsync(UserId, id, clock.GetUtcNow(), cancellationToken);
    return booking is null ? NotFound() : Ok(booking);
  }

  [HttpGet("scheduled-trips"), Authorize(Policy = PartnerPortalAuthorization.ReadScheduledTripsPolicy)]
  public async Task<ActionResult<PartnerRecordsPage<PartnerPortalTrip>>> Trips([FromQuery] PartnerRecordsQuery query,
    [FromQuery] DateOnly? date, [FromQuery] TimeOnly? time, [FromQuery] Guid? originTerminalId,
    [FromQuery] Guid? destinationTerminalId, CancellationToken cancellationToken)
  {
    if (date == DateOnly.MinValue || date == DateOnly.MaxValue || originTerminalId == Guid.Empty || destinationTerminalId == Guid.Empty
      || time.HasValue && time.Value.Ticks % TimeSpan.TicksPerHour != 0) return BadRequest();
    var selectedDate = date ?? DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(8)).DateTime);
    return Ok(await portal.TripsAsync(UserId, query.Page, query.PageSize, selectedDate, time, originTerminalId, destinationTerminalId, cancellationToken));
  }

  [HttpGet("scheduled-trips/filter-options"), Authorize(Policy = PartnerPortalAuthorization.ReadScheduledTripsPolicy)]
  public async Task<ActionResult<PartnerTripFilterOptions>> TripFilterOptions([FromQuery] DateOnly? date, CancellationToken cancellationToken)
  {
    if (date == DateOnly.MinValue || date == DateOnly.MaxValue) return BadRequest();
    var selectedDate = date ?? DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(8)).DateTime);
    return Ok(await portal.TripFilterOptionsAsync(UserId, selectedDate, cancellationToken));
  }

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
