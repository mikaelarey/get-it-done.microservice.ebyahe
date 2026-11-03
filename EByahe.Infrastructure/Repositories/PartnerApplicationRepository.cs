using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerApplicationRepository(EByaheDbContext database) : IPartnerApplicationRepository
{
  public async Task<(IReadOnlyList<PartnerApplication> Items, int TotalCount)> ListForUserAsync(string userId,
    int page, int pageSize, CancellationToken cancellationToken = default)
  {
    var query = database.PartnerApplications.AsNoTracking().Where(application => application.CreatedBy == userId);
    var total = await query.CountAsync(cancellationToken);
    var items = await query.OrderByDescending(application => application.SubmittedAtUtc).ThenByDescending(application => application.Id)
      .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    return (items, total);
  }

  public Task<PartnerApplication?> GetForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default) =>
    database.PartnerApplications.AsNoTracking().Include(application => application.Documents)
      .SingleOrDefaultAsync(application => application.Id == id && application.CreatedBy == userId, cancellationToken);

  public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
    database.PartnerApplications.AsNoTracking().AnyAsync(application => application.Id == id, cancellationToken);

  public async Task AddAsync(PartnerApplication application, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(application.CreatedBy))
      throw new ArgumentException("An authenticated application creator is required.", nameof(application));
    database.PartnerApplications.Add(application);
    database.PartnerApplicationApprovals.Add(new PartnerApplicationApproval
    {
      Id = Guid.NewGuid(), PartnerApplicationId = application.Id, Status = "Pending",
      ChangedBy = application.CreatedBy, DateTimeCreated = application.SubmittedAtUtc
    });
    // The application, documents, initial approval state, and automatic JSON history commit together.
    await database.SaveChangesAsync(cancellationToken);
  }
}
