using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IBusBookingRepository
{
  Task<BusBooking?> GetForUserAsync(Guid id, string userId, CancellationToken cancellationToken);
  Task<BusBooking?> GetByReferenceForUserAsync(string reference, string userId, CancellationToken cancellationToken);
  Task<(IReadOnlyList<BusBooking> Items, int TotalCount)> ListForUserAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
  Task<BusBooking?> GetForRequestAsync(Guid requestId, string userId, CancellationToken cancellationToken);
  // Reserves the requested quantity on the exact departure and saves atomically.
  // Existing request keys return their saved booking without decrementing inventory.
  Task<BusBooking> ReserveAndAddAsync(BusBooking booking, ScheduledTrip expectedTrip, int seats, CancellationToken cancellationToken);
}
