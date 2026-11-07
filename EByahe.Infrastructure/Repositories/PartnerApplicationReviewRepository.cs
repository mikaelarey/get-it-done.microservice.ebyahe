using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EByahe.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerApplicationReviewRepository(EByaheDbContext database, IOptions<KafkaOptions>? kafka = null) : IPartnerApplicationReviewRepository
{
  public async Task<PartnerApplicationDecisionResult> DecideAsync(Guid id, string status, string reviewer, string? remarks, CancellationToken cancellationToken)
  {
    if (status is not ("UnderReview" or "Approved" or "Rejected")) throw new ArgumentOutOfRangeException(nameof(status));
    if (string.IsNullOrWhiteSpace(reviewer) || reviewer.Length > 255) throw new ArgumentException("A reviewer is required.", nameof(reviewer));
    if (remarks?.Length > 2000 || (status == "Rejected" && string.IsNullOrWhiteSpace(remarks)))
      throw new ArgumentException("Declining requires remarks of at most 2000 characters.", nameof(remarks));
    remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    // Serialize decisions for this application so concurrent reviewers cannot overwrite each other.
    var application = await database.PartnerApplications
      .FromSqlInterpolated($"SELECT * FROM [PartnerApplications] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
      .Include(x => x.Documents).SingleOrDefaultAsync(cancellationToken);
    if (application is null) return new(null);
    if (application.Status == status) return new(application); // A retry must not add another audit entry.
    var expectedStatus = status == "UnderReview" ? "Submitted" : "UnderReview";
    if (application.Status != expectedStatus) return new(application, Conflict: true);
    if (status == "Approved")
    {
      if (string.IsNullOrWhiteSpace(application.CreatedBy))
        return new(application, true, "This application has no authenticated creator. Resolve its owner before approving it.");
      var owner = await database.RefUsers.SingleOrDefaultAsync(x => x.ExternalUserId == application.CreatedBy, cancellationToken);
      if (owner is null)
        return new(application, true, "The applicant's user record is not available yet. Wait for account synchronization and try again.");
      var partner = new Partner
      {
        Id = Guid.NewGuid(), Code = "PT-" + application.Id.ToString("N").ToUpperInvariant(),
        DisplayName = application.BusinessName, OwnerUserId = application.CreatedBy,
        PartnerApplicationId = application.Id, Status = "Approved", IsDemo = false
      };
      partner.UserMemberships.Add(new PartnerUser { RefUserId = owner.Id });
      database.Partners.Add(partner);
      database.OutboxMessages.Add(PartnerApprovalOutbox.Create(partner, application, reviewer,
        kafka?.Value.PartnerApprovalTopic ?? PartnerApprovalOutbox.DefaultTopic));
    }
    var previousApproval = await database.PartnerApplicationApprovals.Where(x => x.PartnerApplicationId == id)
      .OrderByDescending(x => x.DateTimeCreated).Select(x => x.Status).FirstOrDefaultAsync(cancellationToken);
    previousApproval ??= application.Status == "Submitted" ? "Pending" : application.Status;
    application.Status = status;
    var decision = new PartnerApplicationApproval
    {
      Id = Guid.NewGuid(), PartnerApplicationId = id, PreviousStatus = previousApproval,
      Status = status, ChangedBy = reviewer, Remarks = remarks, DateTimeCreated = DateTimeOffset.UtcNow
    };
    database.PartnerApplicationApprovals.Add(decision);
    // Legacy ownerless applications remain reviewable, but have no user to notify.
    if (!string.IsNullOrWhiteSpace(application.CreatedBy))
      database.OutboxMessages.Add(PartnerApplicationStatusOutbox.Create(application, decision,
        kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic));
    // Partner, owner membership, approval, history, and outbox events commit together.
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return new(application);
  }

  public Task<EByahe.Domain.Models.PartnerApplication?> GetAsync(Guid id, CancellationToken cancellationToken) =>
    database.PartnerApplications.AsNoTracking().Include(x => x.Documents)
      .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

  public async Task<PartnerApplicationReviewPage> ListAsync(int page, int pageSize, string? search,
    string? category, string? status, CancellationToken cancellationToken)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50)
      throw new ArgumentOutOfRangeException(nameof(page));
    var all = database.PartnerApplications.AsNoTracking();
    var summary = await all.GroupBy(_ => 1).Select(group => new PartnerApplicationReviewSummary(group.Count(),
      group.Count(x => x.Status == "Submitted" || x.Status == "UnderReview"),
      group.Count(x => x.Status == "Approved"))).SingleOrDefaultAsync(cancellationToken)
      ?? new PartnerApplicationReviewSummary(0, 0, 0);
    var categories = await all.Select(x => x.BusinessCategory).Distinct().OrderBy(x => x).ToArrayAsync(cancellationToken);
    var statuses = await all.Select(x => x.Status).Distinct().OrderBy(x => x).ToArrayAsync(cancellationToken);
    var query = all;
    search = search?.Trim(); category = category?.Trim(); status = status?.Trim();
    if (!string.IsNullOrEmpty(search))
      query = query.Where(x => x.BusinessName.Contains(search) || x.TrackingId.Contains(search)
        || x.RepresentativeName.Contains(search) || x.RepresentativeEmail.Contains(search) || x.Address.Contains(search));
    if (!string.IsNullOrEmpty(category)) query = query.Where(x => x.BusinessCategory == category);
    if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
    var total = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(x => x.SubmittedAtUtc).ThenByDescending(x => x.Id)
      .Skip((page - 1) * pageSize).Take(pageSize)
      .Select(x => new PartnerApplicationReviewItem(x.Id, x.TrackingId, x.BusinessName, x.BusinessCategory,
        x.Address, x.RepresentativeName, x.RepresentativeEmail, x.SubmittedAtUtc, x.Status))
      .ToArrayAsync(cancellationToken);
    return new(items, total, page, pageSize, summary, categories, statuses);
  }
}
