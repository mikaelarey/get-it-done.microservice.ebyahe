using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerApplicationRepository(EByaheDbContext database, IRecordChangePublisher? changes = null) : IPartnerApplicationRepository
{
  public Task<ActivePartnerApplication?> GetActiveForUserAsync(string userId,
    CancellationToken cancellationToken = default) =>
    database.PartnerApplications.AsNoTracking()
      .Where(application => application.CreatedBy == userId
        && (application.Status == "Submitted" || application.Status == "UnderReview"))
      .OrderByDescending(application => application.SubmittedAtUtc).ThenByDescending(application => application.Id)
      .Select(application => new ActivePartnerApplication(application.Id, application.TrackingId, application.Status))
      .FirstOrDefaultAsync(cancellationToken);

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
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    // Serialize only this creator across API instances before audit snapshot reads can take range locks.
    var lockResource = "partner-application:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
      System.Text.Encoding.UTF8.GetBytes(application.CreatedBy)));
    await database.Database.ExecuteSqlInterpolatedAsync($"""
      DECLARE @result int;
      EXEC @result = sys.sp_getapplock @Resource = {lockResource}, @LockMode = 'Exclusive',
        @LockOwner = 'Transaction', @LockTimeout = 15000;
      IF @result < 0 THROW 51001, 'Could not acquire the application submission lock.', 1;
      """, cancellationToken);
    var existing = await GetActiveForUserAsync(application.CreatedBy, cancellationToken);
    if (existing is not null) throw new ActivePartnerApplicationException(existing);
    database.PartnerApplications.Add(application);
    database.PartnerApplicationApprovals.Add(new PartnerApplicationApproval
    {
      Id = Guid.NewGuid(), PartnerApplicationId = application.Id, Status = "Pending",
      ChangedBy = application.CreatedBy, DateTimeCreated = application.SubmittedAtUtc
    });
    // The application, documents, initial approval state, and automatic JSON history commit together.
    try
    {
      await database.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);
      if (changes is not null) await changes.PublishAsync(["applications"], application.CreatedBy, ct: cancellationToken);
    }
    catch (DbUpdateException exception) when (exception.InnerException is SqlException sql
      && sql.Number is 2601 or 2627 && sql.Message.Contains("UX_PartnerApplications_ActiveCreator", StringComparison.Ordinal))
    {
      // The filtered unique index also protects submissions arriving at the same time from different API instances.
      await transaction.RollbackAsync(cancellationToken);
      await transaction.DisposeAsync();
      var active = await GetActiveForUserAsync(application.CreatedBy, cancellationToken);
      // The winning application may already have received a final decision by the time we re-read it.
      throw new ActivePartnerApplicationException(active);
    }
  }
}
