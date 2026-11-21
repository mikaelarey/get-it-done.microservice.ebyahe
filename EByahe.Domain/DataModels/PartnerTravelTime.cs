namespace EByahe.Domain.DataModels;

public sealed record PartnerTravelTime(Guid Id, Guid PartnerId, string Partner, Guid OriginTerminalId, string Origin,
  Guid DestinationTerminalId, string Destination, int? DurationMinutes, bool IsActive);
public sealed record PartnerTravelTimeOptions(IReadOnlyList<PartnerTripWorkspace> Workspaces, IReadOnlyList<PartnerTripTerminal> Terminals);
public sealed record AddPartnerTravelTime(Guid PartnerId, Guid OriginTerminalId, Guid DestinationTerminalId, int DurationMinutes);
public sealed record UpdatePartnerTravelTime(int DurationMinutes, bool IsActive);
