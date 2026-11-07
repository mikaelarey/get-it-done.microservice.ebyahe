namespace EByahe.Domain.Models;

public sealed class Partner
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Code { get; set; } = "";
  public string DisplayName { get; set; } = "";
  public string Status { get; set; } = "Approved";
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
  public string BusClass { get; set; } = "";
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
  public string Status { get; set; } = "Scheduled";
  public bool IsDemo { get; set; }
}
