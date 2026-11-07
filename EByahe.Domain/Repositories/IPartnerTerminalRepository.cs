using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerTerminalRepository
{
  Task<IReadOnlyList<PartnerTerminalWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken);
  Task<IReadOnlyList<PartnerTerminalLocation>> LocationsAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerPortalTerminal?> AddAsync(string userId, AddPartnerTerminal command, CancellationToken cancellationToken);
}
