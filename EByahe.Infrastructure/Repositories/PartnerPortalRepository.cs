using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed partial class PartnerPortalRepository(EByaheDbContext database) : IPartnerPortalRepository
{
  private IQueryable<Guid> PartnerIds(string userId) => database.PartnerUsers.AsNoTracking()
    .Where(member => member.IsActive && member.RefUser.ExternalUserId == userId && member.Partner.Status == "Approved")
    .Select(member => member.PartnerId).Distinct();

  private IQueryable<BusBooking> Bookings(string userId, string? search = null)
  {
    var ids = PartnerIds(userId);
    return SearchBookings(search).AsNoTracking().Where(booking =>
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

  public Task<PartnerRecordsPage<BusBooking>> BookingsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken, string? search = null) =>
    Page(Bookings(userId, search).OrderByDescending(row => row.CreatedAtUtc).ThenBy(row => row.Id), page, pageSize, cancellationToken);

  public async Task<BusBooking?> BookingDetailsAsync(string userId, Guid bookingId, CancellationToken cancellationToken) =>
    await BookingCancellationRemarks.LoadAsync(database, await Bookings(userId).SingleOrDefaultAsync(row => row.Id == bookingId, cancellationToken), cancellationToken);

  public Task<PartnerRecordsPage<PartnerPortalTerminal>> TerminalsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken, string? search = null)
  {
    var ids = PartnerIds(userId);
    var terminals = database.BusTerminals.AsNoTracking().Where(row => ids.Contains(row.PartnerId));
    foreach (var term in PartnerKeywordSearch.Tokens(search))
      terminals = terminals.Where(row => EF.Functions.Collate(row.Name + " " + row.Location.Name + " " + row.Address + ", "
        + (row.Location.Barangay == null ? "" : row.Location.Barangay.Decode + ", ")
        + (row.Location.Municipality == null ? "" : row.Location.Municipality.Decode + ", ")
        + (row.Location.Province == null ? "" : row.Location.Province.Decode + ", ")
        + (row.Location.Region == null ? "" : row.Location.Region.Decode), PartnerKeywordSearch.Collation).Contains(term));
    var query = terminals.OrderBy(row => row.Name).ThenBy(row => row.Id)
      .Select(row => new PartnerPortalTerminal(row.Id, row.Name, row.Location.Name,
        TerminalAddress.Format(row.Address, row.Location.Barangay == null ? null : row.Location.Barangay.Decode,
          row.Location.Municipality == null ? null : row.Location.Municipality.Decode,
          row.Location.Province == null ? null : row.Location.Province.Decode,
          row.Location.Region == null ? null : row.Location.Region.Decode), row.Partner.DisplayName, row.IsActive));
    return Page(query, page, pageSize, cancellationToken);
  }

  private IQueryable<ScheduledTrip> VisibleTrips(string userId, DateOnly date, TimeOnly? time = null)
  {
    var ids = PartnerIds(userId);
    var start = new DateTimeOffset(date.ToDateTime(time ?? TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
    var end = time.HasValue ? start.AddHours(1) : start.AddDays(1);
    return database.ScheduledTrips.AsNoTracking().Where(row => ids.Contains(row.PartnerId)
      && row.DepartureAtUtc >= start && row.DepartureAtUtc < end);
  }

  public async Task<PartnerTripFilterOptions> TripFilterOptionsAsync(string userId, DateOnly date, CancellationToken cancellationToken)
  {
    var trips = VisibleTrips(userId, date);
    var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(8)).ToUniversalTime();
    // Bucket by elapsed hour from Manila midnight, independent of the SQL timestamp's stored offset.
    var hours = await trips.Select(trip => EF.Functions.DateDiffHour(start, trip.DepartureAtUtc)).Distinct()
      .OrderBy(hour => hour).ToArrayAsync(cancellationToken);
    var origins = await trips.Select(trip => new { Id = trip.BusRoute.OriginTerminalId,
      trip.BusRoute.OriginTerminal.Name, Location = trip.BusRoute.OriginTerminal.Location.Name, Partner = trip.Partner.DisplayName }).Distinct()
      .OrderBy(terminal => terminal.Name).ThenBy(terminal => terminal.Partner).ThenBy(terminal => terminal.Id).ToArrayAsync(cancellationToken);
    var destinations = await trips.Select(trip => new { Id = trip.BusRoute.DestinationTerminalId,
      trip.BusRoute.DestinationTerminal.Name, Location = trip.BusRoute.DestinationTerminal.Location.Name, Partner = trip.Partner.DisplayName }).Distinct()
      .OrderBy(terminal => terminal.Name).ThenBy(terminal => terminal.Partner).ThenBy(terminal => terminal.Id).ToArrayAsync(cancellationToken);
    return new(hours.Select(hour => hour.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":00").ToArray(),
      origins.Select(terminal => new PartnerTripFilterTerminal(terminal.Id, terminal.Name, terminal.Location, terminal.Partner)).ToArray(),
      destinations.Select(terminal => new PartnerTripFilterTerminal(terminal.Id, terminal.Name, terminal.Location, terminal.Partner)).ToArray());
  }

  public Task<PartnerRecordsPage<PartnerPortalTrip>> TripsAsync(string userId, int page, int pageSize, DateOnly date,
    TimeOnly? time, Guid? originTerminalId, Guid? destinationTerminalId, CancellationToken cancellationToken)
  {
    var query = VisibleTrips(userId, date, time).Where(row => (!originTerminalId.HasValue || row.BusRoute.OriginTerminalId == originTerminalId)
        && (!destinationTerminalId.HasValue || row.BusRoute.DestinationTerminalId == destinationTerminalId))
      .OrderBy(row => row.DepartureAtUtc).ThenBy(row => row.Id)
      .Select(row => new PartnerPortalTrip(row.Id, row.BusRoute.OriginTerminal.Location.Name, row.BusRoute.DestinationTerminal.Location.Name,
        row.Partner.DisplayName, row.Bus.PlateNumber, row.Bus.BusClass.Decode, row.DepartureAtUtc, row.AvailableSeats, row.BaseFare, row.Status));
    return Page(query, page, pageSize, cancellationToken);
  }

  public Task<PartnerRecordsPage<PartnerPortalUser>> UsersAsync(string userId, int page, int pageSize, bool includeTemporaryPasswords, CancellationToken cancellationToken, string? search = null)
  {
    var ids = PartnerIds(userId);
    var now = DateTimeOffset.UtcNow;
    var users = database.PartnerUsers.AsNoTracking().Where(row => ids.Contains(row.PartnerId));
    foreach (var term in PartnerKeywordSearch.Tokens(search))
      users = users.Where(row => EF.Functions.Collate(row.RefUser.FirstName + " " + row.RefUser.LastName + " " + row.RefUser.Email,
        PartnerKeywordSearch.Collation).Contains(term));
    var query = users
      .OrderBy(row => row.RefUser.FirstName).ThenBy(row => row.RefUser.LastName).ThenBy(row => row.Id)
      .Select(row => new PartnerPortalUser(row.Id, (row.RefUser.FirstName + " " + row.RefUser.LastName).Trim(),
        row.RefUser.Email, row.Partner.DisplayName, row.IsActive,
        includeTemporaryPasswords ? database.TemporaryPasswords
          .Where(password => password.CanReveal && password.AvailableUntilUtc > now &&
            password.Request.Status == "Completed" && password.Request.CreatedBy == userId &&
            password.Request.PartnerId == row.PartnerId && password.Request.RefUserId == row.RefUserId)
          .OrderByDescending(password => password.CreatedAtUtc).ThenBy(password => password.Id)
          .Select(password => (Guid?)password.RequestId).FirstOrDefault() : null,
        row.RefUser.ExternalUserId == userId));
    return Page(query, page, pageSize, cancellationToken);
  }

  public Task<PartnerPortalUserDetails?> UserDetailsAsync(string userId, Guid membershipId, CancellationToken cancellationToken)
  {
    var ids = PartnerIds(userId);
    return database.PartnerUsers.AsNoTracking().Where(row => row.Id == membershipId && ids.Contains(row.PartnerId))
      .Select(row => new PartnerPortalUserDetails(row.Id, row.RefUser.FirstName, row.RefUser.LastName,
        row.RefUser.Email, row.Partner.DisplayName, row.IsActive, row.RefUser.Provider, row.CreatedAtUtc,
        row.RefUser.CreatedAtUtc == default ? null : row.RefUser.CreatedAtUtc))
      .SingleOrDefaultAsync(cancellationToken);
  }

  private static async Task<PartnerRecordsPage<T>> Page<T>(IQueryable<T> query, int page, int pageSize, CancellationToken cancellationToken)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var count = await query.CountAsync(cancellationToken);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
    return new(items, count, page, pageSize);
  }
}
