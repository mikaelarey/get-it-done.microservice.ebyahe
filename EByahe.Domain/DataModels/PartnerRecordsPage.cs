namespace EByahe.Domain.DataModels;

public sealed record PartnerRecordsPage<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record PartnerBusRecord(Guid Id, string FleetNumber, string? PlateNumber,
  string BusClass, int SeatCapacity, bool IsActive);

public sealed record PartnerTerminalRecord(Guid Id, string Name, string Location, string Address, bool IsActive);

public sealed record PartnerBookingRecord(Guid Id, string Reference, string Origin, string Destination,
  DateTimeOffset DepartureAtUtc, string? PlateNumber, int ReservedSeats, decimal Total, string Status);

public sealed record PartnerUserRecord(Guid Id, string FullName, string Email, DateTimeOffset JoinedAtUtc);
