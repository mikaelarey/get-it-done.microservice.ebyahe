using EByahe.Application.Dtos.Requests;

namespace EByahe.Application.Dtos.Responses;

public sealed record BusBookingResponse(Guid Id, string Reference, string Status, DateTimeOffset CreatedAtUtc, decimal Total, int ReservedSeats);

public sealed record BusBookingSummary(Guid Id, string Reference, string Status, DateTimeOffset CreatedAtUtc, decimal Total,
  BusTripRequest Trip, int Passengers, string PaymentMethod, int ReservedSeats);

public sealed record BusBookingPage(IReadOnlyList<BusBookingSummary> Items, int TotalCount, int Page, int PageSize);

public sealed record BusBookingDetailsResponse(Guid Id, string Reference, string Status, DateTimeOffset CreatedAtUtc, decimal Total,
  BusTripRequest Trip, BusBookingDetailsRequest Booking, BusPaymentRequest Payment, BusFareRequest Fare, int ReservedSeats);
