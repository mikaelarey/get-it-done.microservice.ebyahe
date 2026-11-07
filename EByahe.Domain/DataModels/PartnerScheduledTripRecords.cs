namespace EByahe.Domain.DataModels;

public sealed record PartnerTripWorkspace(Guid Id, string Name);
public sealed record PartnerTripBus(Guid Id, Guid PartnerId, string FleetNumber, string? PlateNumber, string BusClass, int SeatCapacity);
public sealed record PartnerTripTerminal(Guid Id, Guid PartnerId, string Name, string Location);
public sealed record PartnerTripRoute(Guid Id, Guid PartnerId, Guid OriginTerminalId, Guid DestinationTerminalId, int DurationMinutes);
public sealed record PartnerTripOptions(IReadOnlyList<PartnerTripWorkspace> Workspaces, IReadOnlyList<PartnerTripBus> Buses,
  IReadOnlyList<PartnerTripTerminal> Terminals, IReadOnlyList<PartnerTripRoute> Routes);
public sealed record AddPartnerScheduledTrip(Guid PartnerId, Guid BusId, Guid OriginTerminalId, Guid DestinationTerminalId,
  DateTimeOffset DepartureAtUtc, int DurationMinutes, decimal BaseFare);
