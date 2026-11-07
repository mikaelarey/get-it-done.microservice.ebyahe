using EByahe.Domain.DataModels;
using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IPartnerPortalRepository
{
  Task<PartnerDashboard> DashboardAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<BusBooking>> BookingsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerPortalTerminal>> TerminalsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerPortalTrip>> TripsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerPortalUser>> UsersAsync(string userId, int page, int pageSize, bool includeTemporaryPasswords, CancellationToken cancellationToken);
}
