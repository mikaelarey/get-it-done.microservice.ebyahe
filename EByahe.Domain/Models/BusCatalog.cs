namespace EByahe.Domain.Models;

public sealed class Partner
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Code { get; set; } = "";
  public string DisplayName { get; set; } = "";
  public int CodeDecodePartnerStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("PartnerStatuses", CodeDecodePartnerStatus); set => CodeDecodePartnerStatus = CodeDecodeValues.Code("PartnerStatuses", value); }
  public string? OwnerUserId { get; set; }
  public Guid? PartnerApplicationId { get; set; }
  public bool IsDemo { get; set; }
  public ICollection<PartnerUser> UserMemberships { get; set; } = new List<PartnerUser>();
}

public sealed class Location
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Code { get; set; } = "";
  public string Name { get; set; } = "";
  public string CountryCode { get; set; } = "PH";
  public int? CodeDecodeRegion { get; set; }
  public CodeDecodeRegion? Region { get; set; }
  public int? CodeDecodeProvince { get; set; }
  public CodeDecodeProvince? Province { get; set; }
  public int? CodeDecodeMunicipality { get; set; }
  public CodeDecodeMunicipality? Municipality { get; set; }
  public int? CodeDecodeBarangay { get; set; }
  public CodeDecodeBarangay? Barangay { get; set; }
  public bool IsActive { get; set; } = true;
  public bool IsDemo { get; set; }
}

public sealed class BusTerminal
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid PartnerId { get; set; }
  public Partner Partner { get; set; } = null!;
  public Guid LocationId { get; set; }
  public Location Location { get; set; } = null!;
  public string Name { get; set; } = "";
  public string Address { get; set; } = "";
  public bool IsActive { get; set; } = true;
  public bool IsDemo { get; set; }
}

public sealed class Bus
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid PartnerId { get; set; }
  public Partner Partner { get; set; } = null!;
  public string FleetNumber { get; set; } = "";
  public string? PlateNumber { get; set; }
  public int CodeDecodeBusClass { get; set; }
  public CodeDecodeBusClass BusClass { get; set; } = null!;
  public int SeatCapacity { get; set; } = 40;
  public bool IsActive { get; set; } = true;
  public bool IsDemo { get; set; }
}

public sealed class BusRoute
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid PartnerId { get; set; }
  public Partner Partner { get; set; } = null!;
  public Guid OriginTerminalId { get; set; }
  public BusTerminal OriginTerminal { get; set; } = null!;
  public Guid DestinationTerminalId { get; set; }
  public BusTerminal DestinationTerminal { get; set; } = null!;
  public int DurationMinutes { get; set; }
  public bool IsActive { get; set; } = true;
  public bool IsDemo { get; set; }
}

public sealed class ScheduledTrip
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid PartnerId { get; set; }
  public Partner Partner { get; set; } = null!;
  public Guid BusId { get; set; }
  public Bus Bus { get; set; } = null!;
  public Guid BusRouteId { get; set; }
  public BusRoute BusRoute { get; set; } = null!;
  public DateTimeOffset DepartureAtUtc { get; set; }
  public decimal BaseFare { get; set; }
  // Remaining seats for this departure. Null means inventory has not been configured.
  public int? AvailableSeats { get; set; }
  public int CodeDecodeScheduledTripStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("ScheduledTripStatuses", CodeDecodeScheduledTripStatus); set => CodeDecodeScheduledTripStatus = CodeDecodeValues.Code("ScheduledTripStatuses", value); }
  public bool IsDemo { get; set; }
}
