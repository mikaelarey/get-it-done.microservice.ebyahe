namespace EByahe.Domain.DataModels;

public sealed record PartnerDashboard(int PartnerCount, int BookingCount, int TerminalCount, int ScheduledTripCount, int ActiveUserCount);
public sealed record PartnerPortalTerminal(Guid Id, string Name, string Location, string Address, string Partner, bool IsActive);
public sealed record PartnerPortalTrip(Guid Id, string Origin, string Destination, string Partner, string? PlateNumber,
  string BusClass, DateTimeOffset DepartureAtUtc, int? AvailableSeats, decimal Fare, string Status);
public sealed record PartnerPortalUser(Guid Id, string Name, string Email, string Partner, bool IsActive);
