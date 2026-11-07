using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using EByahe.Application.Interfaces;
using EByahe.Domain.Models;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;

namespace EByahe.Application.Services;

public sealed class BusBookingService(IBusBookingRepository bookings, IBusCatalogRepository catalog, TimeProvider clock) : IBusBookingService
{
  public async Task<(BusBookingResponse Booking, bool Created)> SubmitAsync(CreateBusBookingRequest request, string userId, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(userId) || userId.Length > 255 || userId != userId.Trim())
      throw new ArgumentException("An authenticated user ID is required.", nameof(userId));
    var snapshot = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    var existing = await bookings.GetForRequestAsync(request.RequestId, userId, cancellationToken);
    if (existing is not null) return (Existing(existing, hash), false);
    try { return await CreateAsync(request, userId, snapshot, hash, cancellationToken); }
    catch (Exception error) when (error is SeatReservationException or BookingTripValidationException)
    {
      // An identical concurrent request may have exhausted the trip after the first lookup.
      existing = await bookings.GetForRequestAsync(request.RequestId, userId, cancellationToken);
      if (existing is not null) return (Existing(existing, hash), false);
      throw;
    }
  }

  private async Task<(BusBookingResponse Booking, bool Created)> CreateAsync(CreateBusBookingRequest request,
    string userId, string snapshot, string hash, CancellationToken cancellationToken)
  {
    var scheduled = await catalog.GetAsync(request.Trip.TripId, cancellationToken)
      ?? throw new SeatReservationException("This trip is no longer available. Please search again.");
    var trip = BusTripResponse.From(scheduled);
    if (trip.AvailableSeats is { } seats && request.Booking.Passengers.Count > seats)
      throw new SeatReservationException("There are not enough available seats for these passengers. Please search again.");
    if (request.Trip.Origin != trip.Origin || request.Trip.Destination != trip.Destination
      || request.Trip.Operator != trip.Operator || request.Trip.BusClass != trip.Seat
      || (request.Trip.PlateNumber is not null && request.Trip.PlateNumber != trip.PlateNumber)
      || request.Trip.DepartureDate != trip.DepartureDate || request.Trip.DepartureTime != trip.DepartureTime
      || request.Trip.TravelTime != trip.Duration || request.Booking.Boarding != trip.OriginStation
      || request.Booking.Dropping != trip.DestinationStation || request.Booking.Passengers.Count > trip.Capacity)
      throw new BookingTripValidationException("The submitted trip details do not match the scheduled trip. Please search again.");
    for (var index = 0; index < request.Booking.Passengers.Count; index++)
    {
      var expectedFare = Math.Round(trip.Fare * (request.Booking.Passengers[index].Type == "adult" ? 1m : .8m), 2, MidpointRounding.AwayFromZero);
      if (request.Fare.Fares[index].Amount != expectedFare)
        throw new BookingTripValidationException("The fare has changed. Please search again to review the current fare.");
    }
    // Fare labels identify passengers, not unassigned seat numbers.
    for (var index = 0; index < request.Booking.Passengers.Count; index++)
    {
      var type = request.Booking.Passengers[index].Type switch { "student" => "Student", "senior" => "Senior / PWD", _ => "Adult" };
      request.Fare.Fares[index].Label = $"Passenger {index + 1} \u00b7 {type}";
    }
    // Capture the assigned bus plate from the catalog, including for older clients that omit it.
    request.Trip.PlateNumber = trip.PlateNumber;
    snapshot = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    var id = Guid.NewGuid();
    var booking = new BusBooking
    {
      Id = id, Reference = $"BK-{id:N}".ToUpperInvariant(), CreatedBy = userId,
      RequestId = request.RequestId, CreatedAtUtc = clock.GetUtcNow(), Total = request.Fare.Total,
      RequestHash = hash, DetailsJson = snapshot, ScheduledTripId = scheduled.Id
    };
    var saved = await bookings.ReserveAndAddAsync(booking, scheduled, request.Booking.Passengers.Count, cancellationToken);
    return (Existing(saved, hash), saved.Id == id);
  }

  public async Task<BusBookingDetailsResponse?> GetAsync(Guid id, string userId, CancellationToken cancellationToken) =>
    await bookings.GetForUserAsync(id, userId, cancellationToken) is { } booking ? Details(booking) : null;

  public async Task<BusBookingDetailsResponse?> GetByReferenceAsync(string reference, string userId, CancellationToken cancellationToken) =>
    await bookings.GetByReferenceForUserAsync(reference, userId, cancellationToken) is { } booking ? Details(booking) : null;

  public async Task<BusBookingPage> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
  {
    var result = await bookings.ListForUserAsync(userId, page, pageSize, cancellationToken);
    return new BusBookingPage(result.Items.Select(booking =>
    {
      var snapshot = Snapshot(booking);
      return new BusBookingSummary(booking.Id, booking.Reference, booking.Status, booking.CreatedAtUtc, booking.Total,
        snapshot.Trip, snapshot.Booking.Passengers.Count, snapshot.Payment.Provider, booking.ReservedSeats);
    }).ToArray(), result.TotalCount, page, pageSize);
  }

  private static CreateBusBookingRequest Snapshot(BusBooking booking) =>
    JsonSerializer.Deserialize<CreateBusBookingRequest>(booking.DetailsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
      ?? throw new InvalidOperationException("The saved booking snapshot is unavailable.");

  private static BusBookingDetailsResponse Details(BusBooking booking)
  {
    var snapshot = Snapshot(booking);
    return new(booking.Id, booking.Reference, booking.Status, booking.CreatedAtUtc, booking.Total,
      snapshot.Trip, snapshot.Booking, snapshot.Payment, snapshot.Fare, booking.ReservedSeats);
  }

  private static BusBookingResponse Existing(BusBooking booking, string hash) => booking.RequestHash == hash
    ? Response(booking) : throw new BookingRequestConflictException();
  private static BusBookingResponse Response(BusBooking booking) => new(booking.Id, booking.Reference, booking.Status, booking.CreatedAtUtc, booking.Total, booking.ReservedSeats);
}

public sealed class BookingRequestConflictException : Exception;
public sealed class BookingTripValidationException(string message) : Exception(message);
