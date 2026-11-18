namespace EByahe.Domain.DataModels;

public sealed record PartnerPortalBus(Guid Id, Guid PartnerId, string Partner, string FleetNumber,
  string? PlateNumber, string BusClass, int SeatCapacity, bool IsActive);
public sealed record PartnerBusWorkspace(Guid Id, string Name);
public sealed record BusClassOption(int Code, string Decode);
public sealed record AddPartnerBus(Guid PartnerId, string FleetNumber, string PlateNumber, string BusClass, int SeatCapacity);
public sealed record PartnerBusImportRow(int Row, AddPartnerBus Bus);
public sealed record PartnerBusImportError(int Row, string Field, string Message);
public sealed record PartnerBusImportResult(int ImportedCount, IReadOnlyList<PartnerBusImportError> Errors);
