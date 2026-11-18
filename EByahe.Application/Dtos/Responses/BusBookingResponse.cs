using EByahe.Application.Dtos.Requests;
using EByahe.Domain.Models;
using System.Text.Json;

namespace EByahe.Application.Dtos.Responses;

public sealed record BusBookingResponse(Guid Id, string Reference, string Status, DateTimeOffset CreatedAtUtc, decimal Total, int ReservedSeats, string PaymentStatus = "Pending Payment", string? PaymentType = null, bool PaymentReviewRequired = false);

public sealed record BusBookingSummary(Guid Id, string Reference, string Status, DateTimeOffset CreatedAtUtc, decimal Total,
  BusTripRequest Trip, int Passengers, string PaymentMethod, int ReservedSeats, string PaymentStatus = "Pending Payment", string? PaymentType = null, bool PaymentReviewRequired = false);

public sealed record BusBookingPage(IReadOnlyList<BusBookingSummary> Items, int TotalCount, int Page, int PageSize);

public sealed record BusBookingDetailsResponse(Guid Id, string Reference, string Status, DateTimeOffset CreatedAtUtc, decimal Total,
  BusTripRequest Trip, BusBookingDetailsRequest Booking, BusPaymentRequest Payment, BusFareRequest Fare, int ReservedSeats, string PaymentStatus = "Pending Payment", string? PaymentType = null, bool PaymentReviewRequired = false)
{
  public static BusBookingDetailsResponse From(BusBooking booking)
  {
    var snapshot = JsonSerializer.Deserialize<CreateBusBookingRequest>(booking.DetailsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
      ?? throw new InvalidOperationException("The saved booking snapshot is unavailable.");
    return new(booking.Id, booking.Reference, booking.Status, booking.CreatedAtUtc, booking.Total,
      snapshot.Trip, snapshot.Booking, snapshot.Payment, snapshot.Fare, booking.ReservedSeats, booking.PaymentStatus, booking.PaymentType, booking.PaymentReviewRequired);
  }
}
