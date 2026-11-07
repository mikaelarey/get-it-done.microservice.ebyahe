using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class BusCatalogRepository(EByaheDbContext db, TimeProvider clock) : IBusCatalogRepository
{
  private IQueryable<ScheduledTrip> Published()
  {
    var now = clock.GetUtcNow();
    return db.ScheduledTrips.AsNoTracking().Where(x => x.DepartureAtUtc > now && x.Status == "Scheduled" && x.AvailableSeats > 0
      && x.Partner.Status == "Approved" && x.Bus.IsActive && x.BusRoute.IsActive
      && x.BusRoute.OriginTerminal.IsActive && x.BusRoute.DestinationTerminal.IsActive
      && x.BusRoute.OriginTerminal.Location.IsActive && x.BusRoute.DestinationTerminal.Location.IsActive);
  }
  private static IQueryable<ScheduledTrip> Details(IQueryable<ScheduledTrip> query) => query
    .Include(x => x.Partner).Include(x => x.Bus)
    .Include(x => x.BusRoute).ThenInclude(x => x.OriginTerminal).ThenInclude(x => x.Location)
    .Include(x => x.BusRoute).ThenInclude(x => x.DestinationTerminal).ThenInclude(x => x.Location);

  public async Task<IReadOnlyList<Location>> LocationsAsync(Guid? originId, CancellationToken ct)
  {
    var trips = Published();
    var locations = originId.HasValue
      ? trips.Where(x => x.BusRoute.OriginTerminal.LocationId == originId.Value).Select(x => x.BusRoute.DestinationTerminal.Location)
      : trips.Select(x => x.BusRoute.OriginTerminal.Location);
    return await locations.Distinct().OrderBy(x => x.Name).ToArrayAsync(ct);
  }
  public async Task<IReadOnlyList<ScheduledTrip>> SearchAsync(Guid originId, Guid destinationId, DateOnly departure, int passengers, CancellationToken ct)
  {
    var start = new DateTimeOffset(departure.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
    var end = start.AddDays(1);
    return await Details(Published()).Where(x => x.BusRoute.OriginTerminal.LocationId == originId
      && x.BusRoute.DestinationTerminal.LocationId == destinationId && x.DepartureAtUtc >= start && x.DepartureAtUtc < end
      && x.Bus.SeatCapacity >= passengers && (x.AvailableSeats == null || x.AvailableSeats >= passengers))
      .OrderBy(x => x.DepartureAtUtc).ThenBy(x => x.Id).Take(100).ToArrayAsync(ct);
  }
  public Task<ScheduledTrip?> GetAsync(Guid id, CancellationToken ct) => Details(Published()).SingleOrDefaultAsync(x => x.Id == id, ct);
}
