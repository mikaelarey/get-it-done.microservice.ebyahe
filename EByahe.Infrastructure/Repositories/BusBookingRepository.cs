using EByahe.Domain.Models;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class BusBookingRepository(EByaheDbContext database, TimeProvider clock, IRecordChangePublisher? changes = null) : IBusBookingRepository
{
  public Task<BusBooking?> GetForUserAsync(Guid id, string userId, CancellationToken cancellationToken) =>
    database.BusBookings.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id && row.CreatedBy == userId, cancellationToken);

  public Task<BusBooking?> GetByReferenceForUserAsync(string reference, string userId, CancellationToken cancellationToken) =>
    database.BusBookings.AsNoTracking().SingleOrDefaultAsync(row => row.Reference == reference && row.CreatedBy == userId, cancellationToken);

  public async Task<(IReadOnlyList<BusBooking> Items, int TotalCount)> ListForUserAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
  {
    var query = database.BusBookings.AsNoTracking().Where(row => row.CreatedBy == userId);
    var total = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(row => row.CreatedAtUtc).ThenByDescending(row => row.Id)
      .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    return (items, total);
  }

  public Task<BusBooking?> GetForRequestAsync(Guid requestId, string userId, CancellationToken cancellationToken) =>
    database.BusBookings.AsNoTracking().SingleOrDefaultAsync(row => row.RequestId == requestId && row.CreatedBy == userId, cancellationToken);

  public async Task<BusBooking> ReserveAndAddAsync(BusBooking booking, ScheduledTrip expectedTrip, int seats, CancellationToken cancellationToken)
  {
    if (seats is < 1 or > 20 || booking.ScheduledTripId != expectedTrip.Id)
      throw new ArgumentException("A valid seat quantity and matching scheduled trip are required.");
    await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
    // Serialize a customer's retries even when they target different trips. Hashing keeps the
    // lock resource short and avoids exposing user IDs in SQL lock diagnostics.
    var resource = "EByahe.Booking:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
      System.Text.Encoding.UTF8.GetBytes($"{booking.CreatedBy}:{booking.RequestId:N}")));
    await database.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000; IF @result < 0 THROW 50002, 'Could not acquire booking request lock.', 1;", cancellationToken);
    var existing = await GetForRequestAsync(booking.RequestId, booking.CreatedBy, cancellationToken);
    if (existing is not null) return existing;

    // The update lock prevents two submissions from reading the same remaining seats.
    // Repeatable-read isolation also holds the joined catalog reads until commit,
    // without range-locking missing booking request keys across unrelated requests.
    var trip = await database.ScheduledTrips.FromSqlInterpolated(
        $"SELECT * FROM [ScheduledTrips] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {expectedTrip.Id}")
      .Include(x => x.Partner).Include(x => x.Bus)
      .Include(x => x.BusRoute).ThenInclude(x => x.OriginTerminal).ThenInclude(x => x.Location)
      .Include(x => x.BusRoute).ThenInclude(x => x.DestinationTerminal).ThenInclude(x => x.Location)
      .SingleOrDefaultAsync(cancellationToken);
    if (trip is null || !MatchesPublishedTrip(trip, expectedTrip))
      throw new SeatReservationException("This departure has changed or is no longer available. Please search again.");
    if (trip.AvailableSeats is not { } available)
      throw new SeatReservationException("Seat availability is not configured for this departure. Please choose another trip.");
    available = Math.Min(available, trip.Bus.SeatCapacity);
    if (available < seats)
      throw new SeatReservationException("There are not enough seats remaining for this departure. Please search again.");

    trip.AvailableSeats = available - seats;
    booking.ReservedSeats = seats;
    database.BusBookings.Add(booking);
    try
    {
      await database.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
      if (changes is not null) await changes.PublishAsync(["bookings", "scheduled-trips"], booking.CreatedBy, trip.PartnerId, cancellationToken);
      return booking;
    }
    catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
    {
      await transaction.RollbackAsync(cancellationToken);
      await transaction.DisposeAsync();
      database.Entry(booking).State = EntityState.Detached;
      database.Entry(trip).State = EntityState.Detached;
      // A writer from an older deployment may not use the application lock.
      existing = await GetForRequestAsync(booking.RequestId, booking.CreatedBy, cancellationToken);
      if (existing is null) throw;
      return existing;
    }
  }

  private bool MatchesPublishedTrip(ScheduledTrip trip, ScheduledTrip expected) =>
    trip.Status == "Scheduled" && trip.DepartureAtUtc > clock.GetUtcNow() && trip.Partner.Status == "Approved"
    && trip.Bus.IsActive && trip.BusRoute.IsActive && trip.BusRoute.OriginTerminal.IsActive && trip.BusRoute.DestinationTerminal.IsActive
    && trip.BusRoute.OriginTerminal.Location.IsActive && trip.BusRoute.DestinationTerminal.Location.IsActive
    && trip.PartnerId == expected.PartnerId && trip.BusId == expected.BusId && trip.BusRouteId == expected.BusRouteId
    && trip.DepartureAtUtc == expected.DepartureAtUtc && trip.BaseFare == expected.BaseFare
    && trip.Bus.SeatCapacity == expected.Bus.SeatCapacity && trip.Bus.BusClass == expected.Bus.BusClass
    && trip.Bus.PlateNumber == expected.Bus.PlateNumber && trip.Partner.DisplayName == expected.Partner.DisplayName
    && trip.BusRoute.DurationMinutes == expected.BusRoute.DurationMinutes
    && trip.BusRoute.OriginTerminalId == expected.BusRoute.OriginTerminalId && trip.BusRoute.DestinationTerminalId == expected.BusRoute.DestinationTerminalId
    && trip.BusRoute.OriginTerminal.Name == expected.BusRoute.OriginTerminal.Name && trip.BusRoute.DestinationTerminal.Name == expected.BusRoute.DestinationTerminal.Name
    && trip.BusRoute.OriginTerminal.Location.Name == expected.BusRoute.OriginTerminal.Location.Name
    && trip.BusRoute.DestinationTerminal.Location.Name == expected.BusRoute.DestinationTerminal.Location.Name;
}
