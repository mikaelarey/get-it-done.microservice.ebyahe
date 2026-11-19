namespace EByahe.Domain.DataModels;

public sealed record PartnerDashboardAccess(bool Bookings, bool Trips, bool Terminals, bool Users, bool Buses);
public sealed record PartnerDashboardMetrics(int Bookings, decimal BookingValue, int Trips, decimal Occupancy,
  int Confirmed, int Submitted, int Cancelled);
public sealed record PartnerDashboardDay(DateOnly Date, int Bookings);
public sealed record PartnerDashboardDeparture(Guid Id, string Origin, string Destination, string Terminal,
  string BusClass, string? PlateNumber, DateTimeOffset DepartureAtUtc, int Capacity, int ReservedSeats, int? AvailableSeats);
public sealed record PartnerDashboardBooking(Guid Id, string Reference, string? Passenger, string? Origin, string? Destination,
  string? DepartureDate, string? DepartureTime, int Passengers, decimal Total, string Status);
public sealed record PartnerDashboardActivity(string Id, string Title, string Detail, DateTimeOffset AtUtc, string Resource);
public sealed record PartnerDashboardOverview(string Period, DateOnly StartDate, DateOnly EndDate, DateTimeOffset GeneratedAtUtc,
  PartnerDashboardAccess Access, PartnerDashboardMetrics Current, PartnerDashboardMetrics Previous,
  int TerminalCount, int ActiveUserCount, IReadOnlyList<PartnerDashboardDay> Chart,
  IReadOnlyList<PartnerDashboardDeparture> Departures, IReadOnlyList<PartnerDashboardBooking> RecentBookings,
  IReadOnlyList<PartnerDashboardActivity> Activity);
