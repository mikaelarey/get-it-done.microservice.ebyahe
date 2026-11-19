using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerBusRepository
{
  Task<PartnerRecordsPage<PartnerPortalBus>> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken, string? search = null);
  Task<IReadOnlyList<PartnerBusWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken);
  Task<IReadOnlyList<BusClassOption>> ClassesAsync(CancellationToken cancellationToken);
  Task<PartnerPortalBus?> AddAsync(string userId, AddPartnerBus command, CancellationToken cancellationToken);
  Task<PartnerBusForUpdate?> GetForUpdateAsync(string userId, Guid id, CancellationToken cancellationToken);
  Task<PartnerPortalBus?> UpdateAsync(string userId, Guid id, UpdatePartnerBus command, CancellationToken cancellationToken);
  Task<PartnerBusImportResult?> ImportAsync(string userId, Guid partnerId, IReadOnlyList<PartnerBusImportRow> rows, Guid uploadId, CancellationToken cancellationToken);
}
