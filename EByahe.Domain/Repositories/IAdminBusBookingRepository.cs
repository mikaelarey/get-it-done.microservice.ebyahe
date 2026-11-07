using EByahe.Domain.DataModels;

namespace EByahe.Domain.Repositories;

public interface IAdminBusBookingRepository
{
  Task<AdminBusBookingPage> ListAsync(string userId, bool allPartners, int page, int pageSize,
    string? search, string? status, CancellationToken cancellationToken);
}
