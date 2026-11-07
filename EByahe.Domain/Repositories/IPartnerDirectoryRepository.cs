using EByahe.Domain.DataModels;

namespace EByahe.Domain.Repositories;

public interface IPartnerDirectoryRepository
{
  Task<PartnerDirectoryDetails?> GetAsync(Guid id, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerBusRecord>?> BusesAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerTerminalRecord>?> TerminalsAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerBookingRecord>?> BookingsAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken);
  Task<PartnerRecordsPage<PartnerUserRecord>?> UsersAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken);
  Task<PartnerDirectoryPage> ListAsync(int page, int pageSize, string? search,
    string? category, string? status, CancellationToken cancellationToken);
}
