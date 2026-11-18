namespace EByahe.Domain.DataModels;

public sealed record PartnerTerminalWorkspace(Guid Id, string Name);
public sealed record PartnerTerminalLocation(Guid Id, string Name, string CountryCode);
public sealed record GeographyOption(int Code, string Decode);
public sealed record AddPartnerTerminal(Guid PartnerId, int CodeDecodeRegion, int CodeDecodeProvince,
  int CodeDecodeMunicipality, int CodeDecodeBarangay, string Name, string Address);
public sealed record PartnerTerminalForUpdate(Guid Id, Guid PartnerId, string Name, string Address, string Location,
  bool IsActive, int? CodeDecodeRegion, int? CodeDecodeProvince, int? CodeDecodeMunicipality, int? CodeDecodeBarangay);
public sealed record UpdatePartnerTerminal(string Name, string Address, bool IsActive, int? CodeDecodeRegion,
  int? CodeDecodeProvince, int? CodeDecodeMunicipality, int? CodeDecodeBarangay);
public sealed record PartnerTerminalImportRow(int Row, AddPartnerTerminal Terminal);
public sealed record PartnerTerminalImportError(int Row, string Field, string Message);
public sealed record PartnerTerminalImportResult(int ImportedCount, IReadOnlyList<PartnerTerminalImportError> Errors);
