using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using EByahe.Application.Interfaces;
using EByahe.Application.Services;
using EByahe.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EByahe.Application.Controllers;

[ApiController]
[Authorize]
[Route("api/bus-bookings")]
public sealed class BusBookingsController(IBusBookingService bookings) : ControllerBase
{
  [HttpGet]
  [ProducesResponseType<BusBookingPage>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  public async Task<ActionResult<BusBookingPage>> List(
    [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 50)] int pageSize = 10,
    CancellationToken cancellationToken = default)
  {
    Response.Headers.CacheControl = "private, no-store";
    return Ok(await bookings.ListAsync(User.FindFirst("sub")!.Value, page, pageSize, cancellationToken));
  }

  [HttpGet("by-reference/{reference}")]
  [ProducesResponseType<BusBookingDetailsResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<BusBookingDetailsResponse>> ByReference([MaxLength(64)] string reference, CancellationToken cancellationToken)
  {
    Response.Headers.CacheControl = "private, no-store";
    var booking = await bookings.GetByReferenceAsync(reference, User.FindFirst("sub")!.Value, cancellationToken);
    return booking is null ? NotFound() : Ok(booking);
  }

  [HttpPost]
  [Consumes("application/json")]
  [RequestSizeLimit(64 * 1024)]
  [ProducesResponseType<BusBookingResponse>(StatusCodes.Status201Created)]
  [ProducesResponseType<BusBookingResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
  public async Task<ActionResult<BusBookingResponse>> Submit(CreateBusBookingRequest request, CancellationToken cancellationToken)
  {
    try
    {
      var result = await bookings.SubmitAsync(request, User.FindFirst("sub")!.Value, cancellationToken);
      Response.Headers.CacheControl = "private, no-store";
      return result.Created ? CreatedAtAction(nameof(Get), new { id = result.Booking.Id }, result.Booking) : Ok(result.Booking);
    }
    catch (SeatReservationException error)
    {
      return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = error.Message,
        Extensions = { ["code"] = "seats_unavailable" } });
    }
    catch (BookingTripValidationException error)
    {
      ModelState.AddModelError("Trip", error.Message);
      return ValidationProblem(ModelState);
    }
    catch (BookingRequestConflictException)
    {
      return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "This request ID has already been used for different booking details." });
    }
  }

  [HttpGet("{id:guid}")]
  [ProducesResponseType<BusBookingDetailsResponse>(StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<BusBookingDetailsResponse>> Get(Guid id, CancellationToken cancellationToken)
  {
    Response.Headers.CacheControl = "private, no-store";
    var booking = await bookings.GetAsync(id, User.FindFirst("sub")!.Value, cancellationToken);
    return booking is null ? NotFound() : Ok(booking);
  }
}
