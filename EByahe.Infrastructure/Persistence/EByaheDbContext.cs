using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Persistence;

public sealed partial class EByaheDbContext(DbContextOptions<EByaheDbContext> options,
  Microsoft.Extensions.Options.IOptions<EByahe.Infrastructure.Messaging.KafkaOptions>? kafka = null)
  : DbContext(options), IUnitOfWork
{
  public DbSet<RefUser> RefUsers => Set<RefUser>();
  public DbSet<UserUploadBatch> UserUploadBatches => Set<UserUploadBatch>();
  public DbSet<UploadMemberRequest> UploadMemberRequests => Set<UploadMemberRequest>();
  public DbSet<Upload> Uploads => Set<Upload>();
  public DbSet<UploadRecord> UploadRecords => Set<UploadRecord>();
  public DbSet<CodeDecodeUploadType> CodeDecodeUploadTypes => Set<CodeDecodeUploadType>();
  public DbSet<BusLog> BusLogs => Set<BusLog>();
  public DbSet<TerminalLog> TerminalLogs => Set<TerminalLog>();
  public DbSet<ScheduledTripLog> ScheduledTripLogs => Set<ScheduledTripLog>();
  public DbSet<BusBooking> BusBookings => Set<BusBooking>();
  public DbSet<BookingStatus> CodeDecodeBookingStatuses => Set<BookingStatus>();
  public DbSet<PaymentStatus> CodeDecodePaymentStatuses => Set<PaymentStatus>();
  public DbSet<PaymentType> CodeDecodePaymentTypes => Set<PaymentType>();
  public DbSet<BusBookingStatusLog> BusBookingStatusLogs => Set<BusBookingStatusLog>();
  public DbSet<Partner> Partners => Set<Partner>();
  public DbSet<PartnerUser> PartnerUsers => Set<PartnerUser>();
  public DbSet<PartnerMemberRequest> PartnerMemberRequests => Set<PartnerMemberRequest>();
  public DbSet<TemporaryPassword> TemporaryPasswords => Set<TemporaryPassword>();
  public DbSet<Location> Locations => Set<Location>();
  public DbSet<BusTerminal> BusTerminals => Set<BusTerminal>();
  public DbSet<Bus> Buses => Set<Bus>();
  public DbSet<CodeDecodeBusClass> CodeDecodeBusClasses => Set<CodeDecodeBusClass>();
  public DbSet<CodeDecodeRegion> CodeDecodeRegions => Set<CodeDecodeRegion>();
  public DbSet<CodeDecodeProvince> CodeDecodeProvinces => Set<CodeDecodeProvince>();
  public DbSet<CodeDecodeMunicipality> CodeDecodeMunicipalities => Set<CodeDecodeMunicipality>();
  public DbSet<CodeDecodeBarangay> CodeDecodeBarangays => Set<CodeDecodeBarangay>();
  public DbSet<BusRoute> BusRoutes => Set<BusRoute>();
  public DbSet<ScheduledTrip> ScheduledTrips => Set<ScheduledTrip>();
  public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
  public DbSet<Outgoing> Outgoing => Set<Outgoing>();
  public DbSet<Incoming> Incoming => Set<Incoming>();
  public DbSet<IncomingLog> IncomingLogs => Set<IncomingLog>();
  public DbSet<CodeDecodeTopic> CodeDecodeTopics => Set<CodeDecodeTopic>();
  public DbSet<CodeDecodeEntityType> CodeDecodeEntityTypes => Set<CodeDecodeEntityType>();
  public DbSet<CodeDecodeSettlementFrequency> CodeDecodeSettlementFrequencies => Set<CodeDecodeSettlementFrequency>();
  public DbSet<CodeDecodeRole> CodeDecodeRoles => Set<CodeDecodeRole>();
  public DbSet<CodeDecodeBank> CodeDecodeBanks => Set<CodeDecodeBank>();
  public DbSet<CodeDecodeEventType> CodeDecodeEventTypes => Set<CodeDecodeEventType>();
  public DbSet<CodeDecodeDocumentType> CodeDecodeDocumentTypes => Set<CodeDecodeDocumentType>();
  public DbSet<CodeDecodeBusinessCategory> CodeDecodeBusinessCategories => Set<CodeDecodeBusinessCategory>();
  public DbSet<PartnerApplication> PartnerApplications => Set<PartnerApplication>();
  public DbSet<PartnerApplicationDocument> PartnerApplicationDocuments => Set<PartnerApplicationDocument>();
  public DbSet<PartnerApplicationHistory> PartnerApplicationHistories => Set<PartnerApplicationHistory>();
  public DbSet<PartnerApplicationApproval> PartnerApplicationApprovals => Set<PartnerApplicationApproval>();

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.AddInterceptors(new CodeDecodeQueryInterceptor());

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(EByaheDbContext).Assembly);
    CodeDecodeModel.Configure(modelBuilder);
  }

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    EnsureAppendOnlyLogs();
    var ids = ChangedPartnerApplications();
    var bankApplications = ChangeTracker.Entries<PartnerApplication>().Select(entry => entry.Entity).Where(application => application.HasPendingBankName()).ToArray();
    using var transaction = (ids.Length > 0 || bankApplications.Length > 0 || HasTopicChanges()) && Database.CurrentTransaction is null
      ? Database.BeginTransaction(System.Data.IsolationLevel.Serializable) : null;
    foreach (var application in bankApplications) ResolveBank(application);
    ids = ChangedPartnerApplications();
    foreach (var id in ids)
      PreparePartnerHistory(id, PartnerApplications.AsNoTracking().Include(row => row.Documents).SingleOrDefault(row => row.Id == id));
    var events = PrepareActivityEvents();
    int result;
    var topicRecords = PendingTopicRecords();
    try { ResolveTopics(topicRecords); result = base.SaveChanges(acceptAllChangesOnSuccess); transaction?.Commit(); }
    catch { DetachTopicSetups(); DetachActivityEvents(events); throw; }
    foreach (var record in topicRecords) record.ClearPendingTopic();
    foreach (var application in bankApplications) application.ClearPendingBankName();
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
    var bankApplications = ChangeTracker.Entries<PartnerApplication>().Select(entry => entry.Entity).Where(application => application.HasPendingBankName()).ToArray();
    await using var transaction = (ids.Length > 0 || bankApplications.Length > 0 || HasTopicChanges()) && Database.CurrentTransaction is null
      ? await Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
    foreach (var application in bankApplications) await ResolveBankAsync(application, cancellationToken);
    ids = ChangedPartnerApplications();
    foreach (var id in ids)
      PreparePartnerHistory(id, await PartnerApplications.AsNoTracking().Include(row => row.Documents)
        .SingleOrDefaultAsync(row => row.Id == id, cancellationToken));
    var events = PrepareActivityEvents();
    int result;
    var topicRecords = PendingTopicRecords();
    try
    {
      await ResolveTopicsAsync(topicRecords, cancellationToken);
      result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
      if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }
    catch { DetachTopicSetups(); DetachActivityEvents(events); throw; }
    foreach (var record in topicRecords) record.ClearPendingTopic();
    foreach (var application in bankApplications) application.ClearPendingBankName();
    if (acceptAllChangesOnSuccess) pendingPartnerHistories.Clear();
    return result;
  }

  private void EnsureAppendOnlyLogs()
  {
    if (ChangeTracker.Entries().Any(entry => entry.Entity is EByahe.Domain.Models.Outgoing or IncomingLog
        or PartnerApplicationHistory or PartnerApplicationApproval or BusBookingStatusLog or BusLog or TerminalLog or ScheduledTripLog or Upload or UploadRecord or UploadMemberRequest
      && entry.State is EntityState.Modified or EntityState.Deleted))
      throw new InvalidOperationException("Log and approval entries are append-only; add a new status entry instead.");
  }
}
