using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerDirectoryRepository(EByaheDbContext database) : IPartnerDirectoryRepository
{
  public async Task<PartnerRecordsPage<PartnerBusRecord>?> BusesAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePaging(page, pageSize);
    if (!await database.Partners.AnyAsync(x => x.Id == id, cancellationToken)) return null;
    var query = database.Buses.AsNoTracking().Where(x => x.PartnerId == id).OrderBy(x => x.FleetNumber).ThenBy(x => x.Id)
      .Select(x => new PartnerBusRecord(x.Id, x.FleetNumber, x.PlateNumber, x.BusClass, x.SeatCapacity, x.IsActive));
    return await ReadPageAsync(query, page, pageSize, cancellationToken);
  }

  public async Task<PartnerRecordsPage<PartnerTerminalRecord>?> TerminalsAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePaging(page, pageSize);
    if (!await database.Partners.AnyAsync(x => x.Id == id, cancellationToken)) return null;
    var query = database.BusTerminals.AsNoTracking().Where(x => x.PartnerId == id).OrderBy(x => x.Name).ThenBy(x => x.Id)
      .Select(x => new PartnerTerminalRecord(x.Id, x.Name, x.Location.Name, x.Address, x.IsActive));
    return await ReadPageAsync(query, page, pageSize, cancellationToken);
  }

  public async Task<PartnerRecordsPage<PartnerBookingRecord>?> BookingsAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePaging(page, pageSize);
    if (!await database.Partners.AnyAsync(x => x.Id == id, cancellationToken)) return null;
    var query = from booking in database.BusBookings.AsNoTracking()
      join trip in database.ScheduledTrips.AsNoTracking() on booking.ScheduledTripId equals (Guid?)trip.Id
      where trip.PartnerId == id
      orderby booking.CreatedAtUtc descending, booking.Id descending
      select new PartnerBookingRecord(booking.Id, booking.Reference, trip.BusRoute.OriginTerminal.Location.Name,
        trip.BusRoute.DestinationTerminal.Location.Name, trip.DepartureAtUtc, trip.Bus.PlateNumber,
        booking.ReservedSeats, booking.Total, booking.Status);
    return await ReadPageAsync(query, page, pageSize, cancellationToken);
  }

  public async Task<PartnerRecordsPage<PartnerUserRecord>?> UsersAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
  {
    ValidatePaging(page, pageSize);
    if (!await database.Partners.AnyAsync(x => x.Id == id, cancellationToken)) return null;
    var query = database.PartnerUsers.AsNoTracking().Where(x => x.PartnerId == id && x.IsActive)
      .OrderBy(x => x.RefUser.FirstName).ThenBy(x => x.RefUser.LastName).ThenBy(x => x.Id)
      .Select(x => new PartnerUserRecord(x.Id, (x.RefUser.FirstName + " " + x.RefUser.LastName).Trim(), x.RefUser.Email, x.CreatedAtUtc));
    return await ReadPageAsync(query, page, pageSize, cancellationToken);
  }

  private static void ValidatePaging(int page, int pageSize)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
  }

  private static async Task<PartnerRecordsPage<T>> ReadPageAsync<T>(IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
  {
    var total = await query.CountAsync(cancellationToken);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
    return new(items, total, page, pageSize);
  }

  public async Task<PartnerDirectoryDetails?> GetAsync(Guid id, CancellationToken cancellationToken)
  {
    var row = await (from partner in database.Partners.AsNoTracking().Where(x => x.Id == id)
      join application in database.PartnerApplications.AsNoTracking() on partner.PartnerApplicationId equals (Guid?)application.Id into applications
      from application in applications.DefaultIfEmpty()
      join owner in database.RefUsers.AsNoTracking() on partner.OwnerUserId equals owner.ExternalUserId into owners
      from owner in owners.DefaultIfEmpty()
      select new { partner.Id, partner.Code, partner.DisplayName, partner.Status,
        ApplicationId = partner.PartnerApplicationId,
        ApplicationTrackingId = application != null ? application.TrackingId : null,
        BusinessCategory = application != null ? application.BusinessCategory
          : database.Buses.Any(x => x.PartnerId == partner.Id) ? "bus-operator" : null,
        Address = application != null ? application.Address
          : database.BusTerminals.Where(x => x.PartnerId == partner.Id && x.IsActive)
            .OrderBy(x => x.Location.Name).ThenBy(x => x.Id).Select(x => x.Location.Name).FirstOrDefault(),
        RepresentativeName = application != null ? application.RepresentativeName : null,
        RepresentativeEmail = application != null ? application.RepresentativeEmail : null,
        OwnerRefId = owner != null ? (Guid?)owner.Id : null,
        OwnerFullName = owner != null ? (owner.FirstName + " " + owner.LastName).Trim() : null,
        OwnerEmail = owner != null ? owner.Email : null,
        JoinedAtUtc = database.PartnerApplicationApprovals.Where(x => x.PartnerApplicationId == partner.PartnerApplicationId && x.Status == "Approved")
          .Select(x => (DateTimeOffset?)x.DateTimeCreated).Max(),
        BusCount = database.Buses.Count(x => x.PartnerId == partner.Id),
        TerminalCount = database.BusTerminals.Count(x => x.PartnerId == partner.Id),
        ActiveUserCount = database.PartnerUsers.Count(x => x.PartnerId == partner.Id && x.IsActive),
        BookingCount = database.BusBookings.Count(booking => database.ScheduledTrips
          .Any(trip => trip.Id == booking.ScheduledTripId && trip.PartnerId == partner.Id))
      }).SingleOrDefaultAsync(cancellationToken);
    if (row is null) return null;
    var item = new PartnerDirectoryItem(row.Id, row.Code, row.DisplayName, row.BusinessCategory,
      row.Address, row.RepresentativeName ?? row.OwnerFullName, row.RepresentativeEmail ?? row.OwnerEmail,
      row.JoinedAtUtc, row.Status);
    return new(item, row.ApplicationId, row.ApplicationTrackingId,
      row.OwnerRefId is not null ? new PartnerOwnerDetails(row.OwnerFullName!, row.OwnerEmail!) : null,
      row.BusCount, row.TerminalCount, row.ActiveUserCount, row.BookingCount);
  }

  public async Task<PartnerDirectoryPage> ListAsync(int page, int pageSize, string? search,
    string? category, string? status, CancellationToken cancellationToken)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50)
      throw new ArgumentOutOfRangeException(nameof(page));
    var partners = database.Partners.AsNoTracking();
    var summary = await partners.GroupBy(_ => 1).Select(group => new PartnerDirectorySummary(group.Count(),
      group.Count(x => x.Status == "Approved"), group.Count(x => x.Status == "Suspended")))
      .SingleOrDefaultAsync(cancellationToken) ?? new PartnerDirectorySummary(0, 0, 0);
    // Left joins preserve seeded/legacy partners that have no submitted application.
    var all = from partner in partners
      join application in database.PartnerApplications.AsNoTracking() on partner.PartnerApplicationId equals (Guid?)application.Id into applications
      from application in applications.DefaultIfEmpty()
      join owner in database.RefUsers.AsNoTracking() on partner.OwnerUserId equals owner.ExternalUserId into owners
      from owner in owners.DefaultIfEmpty()
      select new { partner.Id, partner.Code, BusinessName = partner.DisplayName,
        BusinessCategory = application != null ? application.BusinessCategory
          : database.Buses.Any(x => x.PartnerId == partner.Id) ? "bus-operator" : null,
        Address = application != null ? application.Address
          : database.BusTerminals.Where(x => x.PartnerId == partner.Id && x.IsActive)
            .OrderBy(x => x.Location.Name).ThenBy(x => x.Id).Select(x => x.Location.Name).FirstOrDefault(),
        ContactName = application != null ? application.RepresentativeName
          : owner != null ? EF.Functions.Collate((owner.FirstName + " " + owner.LastName).Trim(), "DATABASE_DEFAULT") : null,
        ContactEmail = application != null ? application.RepresentativeEmail
          : owner != null ? EF.Functions.Collate(owner.Email, "DATABASE_DEFAULT") : null,
        JoinedAtUtc = database.PartnerApplicationApprovals.Where(x => x.PartnerApplicationId == partner.PartnerApplicationId && x.Status == "Approved")
          .Select(x => (DateTimeOffset?)x.DateTimeCreated).Max(), partner.Status };
    var categories = await all.Where(x => x.BusinessCategory != null).Select(x => x.BusinessCategory!)
      .Distinct().OrderBy(x => x).ToArrayAsync(cancellationToken);
    var statuses = await partners.Select(x => x.Status).Distinct().OrderBy(x => x).ToArrayAsync(cancellationToken);
    var query = all;
    search = search?.Trim(); category = category?.Trim(); status = status?.Trim();
    if (!string.IsNullOrEmpty(search))
      query = query.Where(x => x.BusinessName.Contains(search) || x.Code.Contains(search)
        || x.BusinessCategory != null && x.BusinessCategory.Contains(search)
        || x.Address != null && x.Address.Contains(search)
        || x.ContactName != null && x.ContactName.Contains(search)
        || x.ContactEmail != null && x.ContactEmail.Contains(search));
    if (!string.IsNullOrEmpty(category)) query = query.Where(x => x.BusinessCategory == category);
    if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
    var total = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(x => x.JoinedAtUtc).ThenBy(x => x.BusinessName).ThenBy(x => x.Id)
      .Skip((page - 1) * pageSize).Take(pageSize)
      .Select(x => new PartnerDirectoryItem(x.Id, x.Code, x.BusinessName, x.BusinessCategory,
        x.Address, x.ContactName, x.ContactEmail, x.JoinedAtUtc, x.Status)).ToArrayAsync(cancellationToken);
    return new(items, total, page, pageSize, summary, categories, statuses);
  }
}
