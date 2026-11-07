using System.Globalization;
using EByahe.Domain.Models;

namespace EByahe.Application.Dtos.Responses;

public sealed record BusLocationResponse(Guid Id, string Name);
public sealed record BusTripResponse(Guid Id, string Origin, string Destination, string Operator,
  string OriginStation, string DestinationStation, string Seat, string Duration,
  string DepartureDate, string DepartureTime, decimal Fare, int Capacity, bool IsDemo, string? PlateNumber, int? AvailableSeats)
{
  public static BusTripResponse From(ScheduledTrip trip)
  {
    var departure = trip.DepartureAtUtc.ToOffset(TimeSpan.FromHours(8));
    var minutes = trip.BusRoute.DurationMinutes;
    var duration = minutes % 60 == 0 ? $"{minutes / 60} hrs" : $"{minutes / 60} hrs {minutes % 60} min";
    return new(trip.Id, trip.BusRoute.OriginTerminal.Location.Name, trip.BusRoute.DestinationTerminal.Location.Name,
      trip.Partner.DisplayName, trip.BusRoute.OriginTerminal.Name, trip.BusRoute.DestinationTerminal.Name,
      trip.Bus.BusClass, duration, departure.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      departure.ToString("HH:mm", CultureInfo.InvariantCulture), trip.BaseFare, trip.Bus.SeatCapacity, trip.IsDemo, trip.Bus.PlateNumber,
      trip.AvailableSeats is { } seats ? Math.Min(seats, trip.Bus.SeatCapacity) : null);
  }
}
