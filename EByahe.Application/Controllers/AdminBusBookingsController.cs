using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController]
[Authorize(Policy = BookingReadAuthorization.Policy)]
[Route("api/admin/bus-bookings")]
public sealed class AdminBusBookingsController(IAdminBusBookingRepository bookings) : ControllerBase
{
  [HttpGet]
  [ProducesResponseType<AdminBusBookingsResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<AdminBusBookingsResponse>> List([FromQuery] AdminBusBookingsQuery query,
    CancellationToken cancellationToken)
  {
    Response.Headers.CacheControl = "private, no-store";
    var result = await bookings.ListAsync(User.FindFirst("sub")!.Value,
      PartnerApplicationReviewAuthorization.HasAccess(User), query.Page, query.PageSize, query.Search, query.Status, cancellationToken);
    return Ok(new AdminBusBookingsResponse(result.Items.Select(AdminBusBookingSummary.From).ToArray(),
      result.TotalCount, result.Page, result.PageSize, result.Statuses));
  }
}
