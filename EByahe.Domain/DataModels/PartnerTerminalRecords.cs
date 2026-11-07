namespace EByahe.Domain.DataModels;

public sealed record PartnerTerminalWorkspace(Guid Id, string Name);
public sealed record PartnerTerminalLocation(Guid Id, string Name, string CountryCode);
public sealed record AddPartnerTerminal(Guid PartnerId, Guid LocationId, string Name, string Address);
