using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerMemberRepository
{
  Task<IReadOnlyList<PartnerMemberWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerMemberStatus?> QueueAsync(string userId, PartnerMemberCommand command, CancellationToken cancellationToken);
  Task<PartnerMemberStatus?> StatusAsync(string userId, Guid id, CancellationToken cancellationToken);
  Task<IReadOnlyList<PartnerMemberStatus>> RecentAsync(string userId, CancellationToken cancellationToken);
  Task CompleteAsync(ConsumedRecord record, CancellationToken cancellationToken);
  Task<PartnerTemporaryPassword?> TemporaryPasswordAsync(string userId, Guid requestId, CancellationToken cancellationToken);
}
