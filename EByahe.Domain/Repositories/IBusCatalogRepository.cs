using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IBusCatalogRepository
{
  Task<IReadOnlyList<Location>> LocationsAsync(Guid? originId, CancellationToken ct);
  Task<IReadOnlyList<ScheduledTrip>> SearchAsync(Guid originId, Guid destinationId, DateOnly departure, int passengers, CancellationToken ct);
  Task<ScheduledTrip?> GetAsync(Guid id, CancellationToken ct);
}
