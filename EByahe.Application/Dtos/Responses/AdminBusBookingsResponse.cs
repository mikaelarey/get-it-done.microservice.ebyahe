using System.Text.Json;
using EByahe.Domain.Models;

namespace EByahe.Application.Dtos.Responses;

public sealed record AdminBusBookingSummary(Guid Id, string Reference, string Status, DateTimeOffset CreatedAtUtc,
  string? Origin, string? Destination, string? Operator, string? PlateNumber, string? DepartureDate,
  string? DepartureTime, int Passengers, int ReservedSeats, decimal Total)
{
  public static AdminBusBookingSummary From(BusBooking booking)
  {
    using var json = JsonDocument.Parse(booking.DetailsJson);
    var hasTrip = json.RootElement.TryGetProperty("trip", out var trip) && trip.ValueKind == JsonValueKind.Object;
    string? Text(string name) => hasTrip && trip.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
      ? value.GetString() : null;
    var passengers = json.RootElement.TryGetProperty("booking", out var details) && details.ValueKind == JsonValueKind.Object
      && details.TryGetProperty("passengers", out var people) && people.ValueKind == JsonValueKind.Array
        ? people.GetArrayLength() : booking.ReservedSeats;
    return new(booking.Id, booking.Reference, booking.Status, booking.CreatedAtUtc, Text("origin"), Text("destination"),
      Text("operator"), Text("plateNumber"), Text("departureDate"), Text("departureTime"), passengers, booking.ReservedSeats, booking.Total);
  }
}

public sealed record AdminBusBookingsResponse(IReadOnlyList<AdminBusBookingSummary> Items, int TotalCount,
  int Page, int PageSize, IReadOnlyList<string> Statuses);
