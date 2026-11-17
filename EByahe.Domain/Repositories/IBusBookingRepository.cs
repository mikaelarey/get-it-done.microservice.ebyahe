using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IBusBookingRepository
{
  Task<IReadOnlyList<BusBookingStatusLog>> HistoryAsync(Guid id, string userId, CancellationToken cancellationToken);
  Task<BusBooking?> GetForUserAsync(Guid id, string userId, CancellationToken cancellationToken);
  Task<BusBooking?> GetByReferenceForUserAsync(string reference, string userId, CancellationToken cancellationToken);
  Task<(IReadOnlyList<BusBooking> Items, int TotalCount)> ListForUserAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
  Task<BusBooking?> GetForRequestAsync(Guid requestId, string userId, CancellationToken cancellationToken);
  // Saves a submitted booking without reserving inventory.
  // Existing request keys return their saved booking without decrementing inventory.
  Task<BusBooking> AddSubmittedAsync(BusBooking booking, ScheduledTrip expectedTrip, CancellationToken cancellationToken);
}
