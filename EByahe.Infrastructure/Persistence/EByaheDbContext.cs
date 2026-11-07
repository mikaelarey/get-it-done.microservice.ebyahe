using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Persistence;

public sealed partial class EByaheDbContext(DbContextOptions<EByaheDbContext> options,
  Microsoft.Extensions.Options.IOptions<EByahe.Infrastructure.Messaging.KafkaOptions>? kafka = null)
  : DbContext(options), IUnitOfWork
{
  public DbSet<RefUser> RefUsers => Set<RefUser>();
  public DbSet<BusBooking> BusBookings => Set<BusBooking>();
  public DbSet<Partner> Partners => Set<Partner>();
  public DbSet<PartnerUser> PartnerUsers => Set<PartnerUser>();
  public DbSet<PartnerMemberRequest> PartnerMemberRequests => Set<PartnerMemberRequest>();
  public DbSet<TemporaryPassword> TemporaryPasswords => Set<TemporaryPassword>();
  public DbSet<Location> Locations => Set<Location>();
  public DbSet<BusTerminal> BusTerminals => Set<BusTerminal>();
  public DbSet<Bus> Buses => Set<Bus>();
  public DbSet<BusRoute> BusRoutes => Set<BusRoute>();
  public DbSet<ScheduledTrip> ScheduledTrips => Set<ScheduledTrip>();
  public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
  public DbSet<Outgoing> Outgoing => Set<Outgoing>();
  public DbSet<Incoming> Incoming => Set<Incoming>();
  public DbSet<IncomingLog> IncomingLogs => Set<IncomingLog>();
  public DbSet<PartnerApplication> PartnerApplications => Set<PartnerApplication>();
  public DbSet<PartnerApplicationDocument> PartnerApplicationDocuments => Set<PartnerApplicationDocument>();
  public DbSet<PartnerApplicationHistory> PartnerApplicationHistories => Set<PartnerApplicationHistory>();
  public DbSet<PartnerApplicationApproval> PartnerApplicationApprovals => Set<PartnerApplicationApproval>();

  protected override void OnModelCreating(ModelBuilder modelBuilder) =>
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(EByaheDbContext).Assembly);

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    EnsureAppendOnlyLogs();
    var ids = ChangedPartnerApplications();
    using var transaction = ids.Length > 0 && Database.CurrentTransaction is null
      ? Database.BeginTransaction(System.Data.IsolationLevel.Serializable) : null;
    foreach (var id in ids)
      PreparePartnerHistory(id, PartnerApplications.AsNoTracking().Include(row => row.Documents).SingleOrDefault(row => row.Id == id));
    var events = PrepareActivityEvents();
    int result;
    try { result = base.SaveChanges(acceptAllChangesOnSuccess); transaction?.Commit(); }
    catch { DetachActivityEvents(events); throw; }
    if (acceptAllChangesOnSuccess) pendingPartnerHistories.Clear();
    return result;
  }

  public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
    SaveChangesAsync(true, cancellationToken);

  public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
    CancellationToken cancellationToken = default)
  {
    EnsureAppendOnlyLogs();
    var ids = ChangedPartnerApplications();
    await using var transaction = ids.Length > 0 && Database.CurrentTransaction is null
      ? await Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
    foreach (var id in ids)
      PreparePartnerHistory(id, await PartnerApplications.AsNoTracking().Include(row => row.Documents)
        .SingleOrDefaultAsync(row => row.Id == id, cancellationToken));
    var events = PrepareActivityEvents();
    int result;
    try
    {
      result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
      if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }
    catch { DetachActivityEvents(events); throw; }
    if (acceptAllChangesOnSuccess) pendingPartnerHistories.Clear();
    return result;
  }

  private void EnsureAppendOnlyLogs()
  {
    if (ChangeTracker.Entries().Any(entry => entry.Entity is EByahe.Domain.Models.Outgoing or IncomingLog
        or PartnerApplicationHistory or PartnerApplicationApproval
      && entry.State is EntityState.Modified or EntityState.Deleted))
      throw new InvalidOperationException("Log and approval entries are append-only; add a new status entry instead.");
  }
}
