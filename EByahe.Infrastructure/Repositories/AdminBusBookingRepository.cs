using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class AdminBusBookingRepository(EByaheDbContext database) : IAdminBusBookingRepository
{
  public async Task<AdminBusBookingPage> ListAsync(string userId, bool allPartners, int page, int pageSize,
    string? search, string? status, CancellationToken cancellationToken)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var query = database.BusBookings.AsNoTracking();
    if (!allPartners)
      query = query.Where(booking => database.ScheduledTrips.Any(trip => trip.Id == booking.ScheduledTripId
        && database.PartnerUsers.Any(member => member.PartnerId == trip.PartnerId && member.IsActive
          && member.RefUser.ExternalUserId == userId)));
    var statuses = await query.Select(booking => booking.Status).Distinct().OrderBy(value => value).ToArrayAsync(cancellationToken);
    if (!string.IsNullOrWhiteSpace(search)) query = query.Where(booking => booking.Reference.Contains(search.Trim()));
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(booking => booking.Status == status.Trim());
    var total = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(booking => booking.CreatedAtUtc).ThenByDescending(booking => booking.Id)
      .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
    return new(items, total, page, pageSize, statuses);
  }
}
