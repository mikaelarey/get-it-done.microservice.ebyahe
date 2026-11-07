using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IPartnerApplicationRepository
{
  Task<EByahe.Domain.DataModels.ActivePartnerApplication?> GetActiveForUserAsync(string userId,
    CancellationToken cancellationToken = default);
  Task AddAsync(PartnerApplication application, CancellationToken cancellationToken = default);
  Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
  Task<(IReadOnlyList<PartnerApplication> Items, int TotalCount)> ListForUserAsync(string userId, int page, int pageSize,
    CancellationToken cancellationToken = default);
  Task<PartnerApplication?> GetForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
