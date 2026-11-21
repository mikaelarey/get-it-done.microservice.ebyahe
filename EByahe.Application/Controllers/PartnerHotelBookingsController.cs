using EByahe.Application.Authorization;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EByahe.Application.Controllers;

[ApiController, Route("api/partner"), Authorize(Policy = PartnerPortalAuthorization.ReadBookingsPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PartnerHotelBookingsController(IPartnerBookingRepository bookings) : ControllerBase
{
  private string UserId => User.FindFirst("sub")!.Value;
  [HttpGet("bookings/types")]
  public Task<PartnerBookingTypes> Types(CancellationToken ct) => bookings.TypesAsync(UserId, ct);
  [HttpGet("hotel-bookings")]
  public Task<PartnerRecordsPage<PartnerHotelBooking>> Hotels([FromQuery] PartnerRecordsQuery query, CancellationToken ct) =>
    bookings.HotelsAsync(UserId, query.Page, query.PageSize, query.Search, ct);
  [HttpGet("hotel-bookings/{id:guid}")]
  public async Task<ActionResult<PartnerHotelBooking>> Details(Guid id, CancellationToken ct) =>
    await bookings.HotelDetailsAsync(UserId, id, ct) is { } booking ? Ok(booking) : NotFound();
}
