using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerBusRepository
{
  Task<PartnerRecordsPage<PartnerPortalBus>> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
  Task<IReadOnlyList<PartnerBusWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerPortalBus?> AddAsync(string userId, AddPartnerBus command, CancellationToken cancellationToken);
}
