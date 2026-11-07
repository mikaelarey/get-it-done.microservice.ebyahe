using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerScheduledTripRepository
{
  Task<PartnerTripOptions> OptionsAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerPortalTrip?> AddAsync(string userId, AddPartnerScheduledTrip command, CancellationToken cancellationToken);
}
