using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerTerminalRepository
{
  Task<IReadOnlyList<PartnerTerminalWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken);
  Task<IReadOnlyList<PartnerTerminalLocation>> LocationsAsync(string userId, CancellationToken cancellationToken);
  Task<IReadOnlyList<GeographyOption>> RegionsAsync(string userId, CancellationToken cancellationToken);
  Task<IReadOnlyList<GeographyOption>> ProvincesAsync(string userId, int region, CancellationToken cancellationToken);
  Task<IReadOnlyList<GeographyOption>> MunicipalitiesAsync(string userId, int province, CancellationToken cancellationToken);
  Task<IReadOnlyList<GeographyOption>> BarangaysAsync(string userId, int municipality, CancellationToken cancellationToken);
  Task<PartnerPortalTerminal?> AddAsync(string userId, AddPartnerTerminal command, CancellationToken cancellationToken);
  Task<PartnerTerminalForUpdate?> GetForUpdateAsync(string userId, Guid id, CancellationToken cancellationToken);
  Task<PartnerPortalTerminal?> UpdateAsync(string userId, Guid id, UpdatePartnerTerminal command, CancellationToken cancellationToken);
  Task<PartnerTerminalImportResult?> ImportAsync(string userId, Guid partnerId, IReadOnlyList<PartnerTerminalImportRow> rows, Guid uploadId, CancellationToken cancellationToken);
}
