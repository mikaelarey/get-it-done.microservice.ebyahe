namespace EByahe.Domain.DataModels;

public sealed record PartnerHotel(Guid Id, Guid PartnerId, string Partner, string Name, string Address,
  int CodeDecodeRegion, string Region, int CodeDecodeProvince, string Province,
  int CodeDecodeMunicipality, string Municipality, int CodeDecodeBarangay, string Barangay,
  string ContactNumber, string Email, string? Description, bool IsActive);
public sealed record PartnerHotelWorkspace(Guid Id, string Name);
public sealed record AddPartnerHotel(Guid PartnerId, string Name, string Address,
  int CodeDecodeRegion, int CodeDecodeProvince, int CodeDecodeMunicipality, int CodeDecodeBarangay,
  string ContactNumber, string Email, string? Description);
