namespace EByahe.Domain.DataModels;

public sealed record PartnerPortalBus(Guid Id, Guid PartnerId, string Partner, string FleetNumber,
  string? PlateNumber, string BusClass, int SeatCapacity, bool IsActive);
public sealed record PartnerBusWorkspace(Guid Id, string Name);
public sealed record AddPartnerBus(Guid PartnerId, string FleetNumber, string PlateNumber, string BusClass, int SeatCapacity);
