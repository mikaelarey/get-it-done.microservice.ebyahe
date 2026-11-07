using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerPortalRepository(EByaheDbContext database) : IPartnerPortalRepository
{
  private IQueryable<Guid> PartnerIds(string userId) => database.PartnerUsers.AsNoTracking()
    .Where(member => member.IsActive && member.RefUser.ExternalUserId == userId && member.Partner.Status == "Approved")
    .Select(member => member.PartnerId).Distinct();

  private IQueryable<BusBooking> Bookings(string userId)
  {
    var ids = PartnerIds(userId);
    return database.BusBookings.AsNoTracking().Where(booking =>
      database.ScheduledTrips.Any(trip => trip.Id == booking.ScheduledTripId && ids.Contains(trip.PartnerId)));
  }

  public async Task<PartnerDashboard> DashboardAsync(string userId, CancellationToken cancellationToken)
  {
    var ids = PartnerIds(userId);
    return new(await ids.CountAsync(cancellationToken), await Bookings(userId).CountAsync(cancellationToken),
      await database.BusTerminals.CountAsync(row => ids.Contains(row.PartnerId), cancellationToken),
      await database.ScheduledTrips.CountAsync(row => ids.Contains(row.PartnerId), cancellationToken),
      await database.PartnerUsers.Where(row => ids.Contains(row.PartnerId) && row.IsActive)
        .Select(row => row.RefUserId).Distinct().CountAsync(cancellationToken));
  }

  public Task<PartnerRecordsPage<BusBooking>> BookingsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken) =>
    Page(Bookings(userId).OrderByDescending(row => row.CreatedAtUtc).ThenBy(row => row.Id), page, pageSize, cancellationToken);

  public Task<PartnerRecordsPage<PartnerPortalTerminal>> TerminalsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
  {
    var ids = PartnerIds(userId);
    var query = database.BusTerminals.AsNoTracking().Where(row => ids.Contains(row.PartnerId)).OrderBy(row => row.Name).ThenBy(row => row.Id)
      .Select(row => new PartnerPortalTerminal(row.Id, row.Name, row.Location.Name, row.Address, row.Partner.DisplayName, row.IsActive));
    return Page(query, page, pageSize, cancellationToken);
  }

  public Task<PartnerRecordsPage<PartnerPortalTrip>> TripsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
  {
    var ids = PartnerIds(userId);
    var query = database.ScheduledTrips.AsNoTracking().Where(row => ids.Contains(row.PartnerId)).OrderByDescending(row => row.DepartureAtUtc).ThenBy(row => row.Id)
      .Select(row => new PartnerPortalTrip(row.Id, row.BusRoute.OriginTerminal.Location.Name, row.BusRoute.DestinationTerminal.Location.Name,
        row.Partner.DisplayName, row.Bus.PlateNumber, row.Bus.BusClass, row.DepartureAtUtc, row.AvailableSeats, row.BaseFare, row.Status));
    return Page(query, page, pageSize, cancellationToken);
  }

  public Task<PartnerRecordsPage<PartnerPortalUser>> UsersAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
  {
    var ids = PartnerIds(userId);
    var query = database.PartnerUsers.AsNoTracking().Where(row => ids.Contains(row.PartnerId))
      .OrderBy(row => row.RefUser.FirstName).ThenBy(row => row.RefUser.LastName).ThenBy(row => row.Id)
      .Select(row => new PartnerPortalUser(row.Id, (row.RefUser.FirstName + " " + row.RefUser.LastName).Trim(),
        row.RefUser.Email, row.Partner.DisplayName, row.IsActive));
    return Page(query, page, pageSize, cancellationToken);
  }

  private static async Task<PartnerRecordsPage<T>> Page<T>(IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var count = await query.CountAsync(cancellationToken);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
    return new(items, count, page, pageSize);
  }
}
