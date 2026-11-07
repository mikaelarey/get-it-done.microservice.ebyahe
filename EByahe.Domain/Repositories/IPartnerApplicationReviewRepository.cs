using EByahe.Domain.DataModels;

namespace EByahe.Domain.Repositories;

public interface IPartnerApplicationReviewRepository
{
  Task<PartnerApplicationDecisionResult> DecideAsync(Guid id, string status, string reviewer, string? remarks, CancellationToken cancellationToken);
  Task<EByahe.Domain.Models.PartnerApplication?> GetAsync(Guid id, CancellationToken cancellationToken);
  Task<PartnerApplicationReviewPage> ListAsync(int page, int pageSize, string? search,
    string? category, string? status, CancellationToken cancellationToken);
}

public sealed record PartnerApplicationDecisionResult(EByahe.Domain.Models.PartnerApplication? Application, bool Conflict = false, string? ConflictReason = null);
