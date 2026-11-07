using System.Data;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using EByahe.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerBusRepository(EByaheDbContext db, IOptions<KafkaOptions>? kafka = null, IRecordChangePublisher? changes = null) : IPartnerBusRepository
{
  private IQueryable<Partner> Accessible(string userId) => db.Partners.Where(partner => partner.Status == "Approved"
    && db.PartnerUsers.Any(member => member.PartnerId == partner.Id && member.IsActive && member.RefUser.ExternalUserId == userId));
  private IQueryable<Partner> BusOperators(string userId) => Accessible(userId).Where(partner => db.PartnerApplications
    .Any(application => application.Id == partner.PartnerApplicationId && application.Status == "Approved" && application.BusinessCategory == "bus-operator"));

  public async Task<PartnerRecordsPage<PartnerPortalBus>> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var ids = Accessible(userId).Select(partner => partner.Id);
    var query = db.Buses.AsNoTracking().Where(bus => ids.Contains(bus.PartnerId));
    var count = await query.CountAsync(cancellationToken);
    var items = await query.OrderBy(bus => bus.Partner.DisplayName).ThenBy(bus => bus.FleetNumber).ThenBy(bus => bus.Id)
      .Skip((page - 1) * pageSize).Take(pageSize)
      .Select(bus => new PartnerPortalBus(bus.Id, bus.PartnerId, bus.Partner.DisplayName, bus.FleetNumber, bus.PlateNumber,
        bus.BusClass, bus.SeatCapacity, bus.IsActive)).ToArrayAsync(cancellationToken);
    return new(items, count, page, pageSize);
  }

  public async Task<IReadOnlyList<PartnerBusWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken) =>
    await BusOperators(userId).AsNoTracking().OrderBy(partner => partner.DisplayName).ThenBy(partner => partner.Id)
      .Select(partner => new PartnerBusWorkspace(partner.Id, partner.DisplayName)).ToArrayAsync(cancellationToken);

  public async Task<PartnerPortalBus?> AddAsync(string userId, AddPartnerBus command, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == command.PartnerId, cancellationToken);
    if (partner is null) return null;
    var bus = new Bus { PartnerId = partner.Id, FleetNumber = command.FleetNumber, PlateNumber = command.PlateNumber,
      BusClass = command.BusClass, SeatCapacity = command.SeatCapacity };
    db.Buses.Add(bus);
    await PartnerCatalogActivityOutbox.QueueAsync(db, partner.Id, userId, "bus.created", bus.Id,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerBusConflictException(); }
    if (changes is not null) await changes.PublishAsync(["buses"], userId, partner.Id, cancellationToken);
    return new(bus.Id, partner.Id, partner.DisplayName, bus.FleetNumber, bus.PlateNumber, bus.BusClass, bus.SeatCapacity, bus.IsActive);
  }
}
