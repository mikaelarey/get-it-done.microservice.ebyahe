using EByahe.Domain.DataModels;
using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IPartnerPortalRepository
{
  Task<PartnerDashboard> DashboardAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerDashboardOverview> DashboardOverviewAsync(string userId, string period, DateTimeOffset now,
    PartnerDashboardAccess access, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<BusBooking>> BookingsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken, string? search = null);
  Task<BusBooking?> BookingDetailsAsync(string userId, Guid bookingId, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerPortalTerminal>> TerminalsAsync(string userId, int page, int pageSize, CancellationToken cancellationToken, string? search = null);
  Task<PartnerRecordsPage<PartnerPortalTrip>> TripsAsync(string userId, int page, int pageSize, DateOnly date, Guid? terminalId, CancellationToken cancellationToken);
  Task<IReadOnlyList<PartnerTripFilterTerminal>> TripFilterTerminalsAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerPortalUser>> UsersAsync(string userId, int page, int pageSize, bool includeTemporaryPasswords, CancellationToken cancellationToken, string? search = null);
  Task<PartnerPortalUserDetails?> UserDetailsAsync(string userId, Guid membershipId, CancellationToken cancellationToken);
}
