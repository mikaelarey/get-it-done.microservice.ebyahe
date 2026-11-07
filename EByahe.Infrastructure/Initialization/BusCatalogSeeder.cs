using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Initialization;

/// <summary>Repeatable local samples; never approves or modifies submitted applications.</summary>
public sealed class BusCatalogSeeder(EByaheDbContext db, TimeProvider clock)
{
  public async Task SeedAsync(CancellationToken ct = default)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'EByahe.DemoCatalog', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000; IF @result < 0 THROW 50001, 'Could not acquire catalog seed lock.', 1;", ct);
    var partners = await db.Partners.Where(x => x.IsDemo).ToListAsync(ct);
    var locations = await db.Locations.Where(x => x.IsDemo).ToListAsync(ct);
    var terminals = await db.BusTerminals.Where(x => x.IsDemo).ToListAsync(ct);
    var buses = await db.Buses.Where(x => x.IsDemo).ToListAsync(ct);
    var uncounted = await db.ScheduledTrips.Include(x => x.Bus).Where(x => x.IsDemo && x.AvailableSeats == null
      && !db.BusBookings.Any(booking => booking.ScheduledTripId == x.Id && booking.ReservedSeats > 0)).ToArrayAsync(ct);
    foreach (var trip in uncounted) trip.AvailableSeats = trip.Bus.SeatCapacity;
    var routes = await db.BusRoutes.Where(x => x.IsDemo).ToListAsync(ct);
    var now = clock.GetUtcNow();
    var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(8)).DateTime);
    var existing = (await db.ScheduledTrips.Where(x => x.IsDemo && x.DepartureAtUtc >= now.AddDays(-1))
      .Select(x => new { x.BusId, x.DepartureAtUtc }).ToListAsync(ct)).Select(x => (x.BusId, x.DepartureAtUtc)).ToHashSet();
    foreach (var option in Options)
    {
      var code = "DEMO-" + Slug(option.Operator);
      var partner = partners.SingleOrDefault(x => x.Code == code);
      if (partner is null)
      {
        partner = new Partner { Code = code, DisplayName = option.Operator, IsDemo = true };
        db.Partners.Add(partner); partners.Add(partner); await db.SaveChangesAsync(ct);
      }
      var endpoints = new List<BusTerminal>();
      foreach (var name in new[] { option.Origin, option.Destination })
      {
        var locationCode = "DEMO-" + Slug(name);
        var location = locations.SingleOrDefault(x => x.Code == locationCode);
        if (location is null)
        {
          location = new Location { Code = locationCode, Name = name, IsDemo = true };
          db.Locations.Add(location); locations.Add(location); await db.SaveChangesAsync(ct);
        }
        var terminal = terminals.SingleOrDefault(x => x.PartnerId == partner.Id && x.LocationId == location.Id);
        if (terminal is null)
        {
          terminal = new BusTerminal { PartnerId = partner.Id, LocationId = location.Id,
            Name = $"{option.Operator} {name} Bus Station", Address = $"Sample terminal, {name}, Philippines", IsDemo = true };
          db.BusTerminals.Add(terminal); terminals.Add(terminal); await db.SaveChangesAsync(ct);
        }
        endpoints.Add(terminal);
      }
      for (var direction = 0; direction < 2; direction++)
      {
        var origin = endpoints[direction]; var destination = endpoints[1 - direction];
        var route = routes.SingleOrDefault(x => x.PartnerId == partner.Id && x.OriginTerminalId == origin.Id && x.DestinationTerminalId == destination.Id);
        if (route is null)
        {
          route = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id,
            DurationMinutes = option.Minutes, IsDemo = true };
          db.BusRoutes.Add(route); routes.Add(route); await db.SaveChangesAsync(ct);
        }
        var fleetNumber = $"DEMO-{option.Number:000}-{direction}";
        var bus = buses.SingleOrDefault(x => x.PartnerId == partner.Id && x.FleetNumber == fleetNumber);
        if (bus is null)
        {
          bus = new Bus { PartnerId = partner.Id, FleetNumber = fleetNumber, BusClass = option.Class, SeatCapacity = 40, IsDemo = true };
          db.Buses.Add(bus); buses.Add(bus); await db.SaveChangesAsync(ct);
        }
        if (string.IsNullOrWhiteSpace(bus.PlateNumber))
          bus.PlateNumber = $"SMP-{1000 + (option.Number - 1) * 2 + direction + 1}";
        for (var day = 0; day < 14; day++)
        {
          var departure = new DateTimeOffset(today.AddDays(day).ToDateTime(Times[(option.Number - 1) % Times.Length]), TimeSpan.FromHours(8)).ToUniversalTime();
          if (!existing.Add((bus.Id, departure))) continue;
          db.ScheduledTrips.Add(new ScheduledTrip { PartnerId = partner.Id, BusId = bus.Id, BusRouteId = route.Id,
            DepartureAtUtc = departure, BaseFare = option.Fare, AvailableSeats = bus.SeatCapacity, IsDemo = true });
        }
      }
    }
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
  }

  private static string Slug(string value) => value.Replace(" ", "-").ToUpperInvariant();
  private sealed record Option(int Number, string Origin, string Destination, string Operator, string Class, int Minutes, decimal Fare);
  private static readonly TimeOnly[] Times = [new(6, 0), new(8, 30), new(10, 0), new(13, 30), new(17, 0), new(21, 30)];
  private static readonly Option[] Options = [
    new(1, "Manila", "Baguio", "Victory Liner", "First Class", 360, 750m),
    new(2, "Manila", "Baguio", "Victory Liner", "Deluxe", 360, 850m),
    new(3, "Manila", "Olongapo", "Victory Liner", "First Class", 240, 280m),
    new(4, "Manila", "Olongapo", "Victory Liner", "Deluxe", 240, 350m),
    new(5, "Manila", "Dagupan", "Victory Liner", "First Class", 300, 450m),
    new(6, "Manila", "Dagupan", "Victory Liner", "Deluxe", 300, 550m),
    new(7, "Manila", "Alaminos", "Victory Liner", "First Class", 360, 550m),
    new(8, "Manila", "Alaminos", "Victory Liner", "Deluxe", 360, 650m),
    new(9, "Cubao", "Vigan", "Partas", "Super Deluxe", 540, 960m),
    new(10, "Cubao", "Vigan", "Partas", "Luxury", 540, 1160m),
    new(11, "Cubao", "Laoag", "Partas", "Luxury", 660, 1392m),
    new(12, "Cubao", "Tuguegarao", "Partas", "Deluxe", 720, 1164m),
    new(13, "Manila", "Batangas City", "ALPS The Bus", "Executive Class", 180, 350m),
    new(14, "Manila", "Batangas City", "ALPS The Bus", "Executive Luxury", 180, 400m),
    new(15, "Manila", "Batangas City", "ALPS The Bus", "Executive Solo", 180, 450m),
    new(16, "Manila", "Naga City", "ALPS The Bus", "Executive Class", 600, 757m),
    new(17, "Manila", "Naga City", "ALPS The Bus", "Executive Luxury", 600, 950m),
    new(18, "Manila", "Naga City", "ALPS The Bus", "Executive Solo", 600, 1200m),
    new(19, "Buendia", "Naga City", "DLTB", "Regular Aircon", 540, 900m),
    new(20, "Buendia", "Naga City", "DLTB", "Deluxe", 540, 1100m),
    new(21, "Buendia", "Naga City", "DLTB", "First Class", 540, 1300m),
    new(22, "Buendia", "Legazpi City", "DLTB", "Regular Aircon", 720, 1200m),
    new(23, "Buendia", "Legazpi City", "DLTB", "Deluxe", 720, 1450m),
    new(24, "Buendia", "Legazpi City", "DLTB", "First Class", 720, 1700m),
    new(25, "Buendia", "Sorsogon", "DLTB", "Regular Aircon", 840, 1400m),
    new(26, "Buendia", "Sorsogon", "DLTB", "Deluxe", 840, 1650m),
    new(27, "Buendia", "Sorsogon", "DLTB", "First Class", 840, 1950m),
    new(28, "Buendia", "Lucena City", "DLTB", "Regular Aircon", 240, 350m),
    new(29, "Buendia", "Lucena City", "DLTB", "Deluxe", 240, 450m),
    new(30, "Cebu City", "Oslob", "Ceres Liner", "Regular / Air-conditioned", 240, 155m),
    new(31, "Cebu City", "Moalboal", "Ceres Liner", "Regular / Air-conditioned", 180, 120m),
    new(32, "Cebu City", "Dumaguete", "Ceres Liner", "Air-conditioned / Direct", 360, 250m),
    new(33, "Cebu City", "Bato", "Ceres Liner", "Regular / Air-conditioned", 300, 180m),
  ];
}
