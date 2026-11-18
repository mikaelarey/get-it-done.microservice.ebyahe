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
        bus.BusClass.Decode, bus.SeatCapacity, bus.IsActive)).ToArrayAsync(cancellationToken);
    return new(items, count, page, pageSize);
  }

  public async Task<IReadOnlyList<PartnerBusWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken) =>
    await BusOperators(userId).AsNoTracking().OrderBy(partner => partner.DisplayName).ThenBy(partner => partner.Id)
      .Select(partner => new PartnerBusWorkspace(partner.Id, partner.DisplayName)).ToArrayAsync(cancellationToken);

  public async Task<IReadOnlyList<BusClassOption>> ClassesAsync(CancellationToken cancellationToken) =>
    await db.CodeDecodeBusClasses.AsNoTracking().OrderBy(row => row.Code)
      .Select(row => new BusClassOption(row.Code, row.Decode)).ToArrayAsync(cancellationToken);

  public async Task<PartnerPortalBus?> AddAsync(string userId, AddPartnerBus command, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == command.PartnerId, cancellationToken);
    if (partner is null) return null;
    var busClass = await db.CodeDecodeBusClasses.SingleOrDefaultAsync(row => row.Decode == command.BusClass, cancellationToken)
      ?? throw new PartnerBusClassValidationException();
    var bus = new Bus { PartnerId = partner.Id, FleetNumber = command.FleetNumber, PlateNumber = command.PlateNumber,
      BusClass = busClass, CodeDecodeBusClass = busClass.Code, SeatCapacity = command.SeatCapacity };
    db.Buses.Add(bus);
    CatalogAudit.Record(db, bus, userId, "Created");
    await PartnerCatalogActivityOutbox.QueueAsync(db, partner.Id, userId, "bus.created", bus.Id,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerBusConflictException(); }
    if (changes is not null) await changes.PublishAsync(["buses"], userId, partner.Id, cancellationToken);
    return new(bus.Id, partner.Id, partner.DisplayName, bus.FleetNumber, bus.PlateNumber, bus.BusClass.Decode, bus.SeatCapacity, bus.IsActive);
  }

  public Task<PartnerBusForUpdate?> GetForUpdateAsync(string userId, Guid id, CancellationToken cancellationToken)
  {
    var ids = BusOperators(userId).Select(partner => partner.Id); var now = DateTimeOffset.UtcNow;
    return db.Buses.AsNoTracking().Where(bus => bus.Id == id && ids.Contains(bus.PartnerId))
      .Select(bus => new PartnerBusForUpdate(bus.Id, bus.PartnerId, bus.Partner.DisplayName, bus.FleetNumber, bus.PlateNumber,
        bus.BusClass.Decode, bus.SeatCapacity, bus.IsActive,
        db.ScheduledTrips.Any(trip => trip.BusId == bus.Id && trip.Status == "Scheduled" && trip.DepartureAtUtc > now)))
      .SingleOrDefaultAsync(cancellationToken);
  }

  public async Task<PartnerPortalBus?> UpdateAsync(string userId, Guid id, UpdatePartnerBus command, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var ids = BusOperators(userId).Select(partner => partner.Id);
    var bus = await db.Buses.Include(row => row.Partner).Include(row => row.BusClass).SingleOrDefaultAsync(row => row.Id == id && ids.Contains(row.PartnerId), cancellationToken);
    if (bus is null) return null;
    var previous = CatalogAudit.Snapshot(bus);
    var busClass = await db.CodeDecodeBusClasses.SingleOrDefaultAsync(row => row.Decode == command.BusClass, cancellationToken)
      ?? throw new PartnerBusClassValidationException();
    // Inventory is set when a trip is scheduled. Keep that capacity stable until its departures finish.
    if (bus.SeatCapacity != command.SeatCapacity && await db.ScheduledTrips.AnyAsync(trip => trip.BusId == id
      && trip.Status == "Scheduled" && trip.DepartureAtUtc > DateTimeOffset.UtcNow, cancellationToken))
      throw new PartnerBusCapacityConflictException();
    bus.FleetNumber = command.FleetNumber; bus.PlateNumber = command.PlateNumber; bus.BusClass = busClass; bus.CodeDecodeBusClass = busClass.Code;
    bus.SeatCapacity = command.SeatCapacity; bus.IsActive = command.IsActive;
    CatalogAudit.Record(db, bus, userId, "Updated", previous);
    await PartnerCatalogActivityOutbox.QueueAsync(db, bus.PartnerId, userId, "bus.updated", bus.Id,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerBusConflictException(); }
    if (changes is not null) await changes.PublishAsync(["buses", "scheduled-trips"], userId, bus.PartnerId, cancellationToken);
    return new(bus.Id, bus.PartnerId, bus.Partner.DisplayName, bus.FleetNumber, bus.PlateNumber, bus.BusClass.Decode, bus.SeatCapacity, bus.IsActive);
  }

  public async Task<PartnerBusImportResult?> ImportAsync(string userId, Guid partnerId, IReadOnlyList<PartnerBusImportRow> rows, Guid uploadId, CancellationToken cancellationToken)
  {
    if (rows.Count is < 1 or > 500 || rows.Any(row => row.Bus.PartnerId != partnerId)) throw new ArgumentException("Invalid bus import batch.");
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == partnerId, cancellationToken);
    if (partner is null) return null;
    if (!await db.Uploads.AnyAsync(upload => upload.Id == uploadId && upload.CreatedBy == userId && upload.UploadType == UploadTypeCodes.Buses, cancellationToken))
      throw new ArgumentException("A matching upload log is required.", nameof(uploadId));
    var plates = rows.Select(row => row.Bus.PlateNumber).ToArray();
    var fleets = rows.Select(row => row.Bus.FleetNumber).ToArray();
    var existing = await db.Buses.AsNoTracking().Where(bus => plates.Contains(bus.PlateNumber!)
      || bus.PartnerId == partnerId && fleets.Contains(bus.FleetNumber))
      .Select(bus => new { bus.PartnerId, bus.FleetNumber, bus.PlateNumber }).ToArrayAsync(cancellationToken);
    var errors = new List<PartnerBusImportError>();
    var classes = await db.CodeDecodeBusClasses.ToArrayAsync(cancellationToken);
    var classByName = classes.ToDictionary(row => row.Decode, StringComparer.OrdinalIgnoreCase);
    foreach (var row in rows)
    {
      if (!classByName.ContainsKey(row.Bus.BusClass)) errors.Add(new(row.Row, "Bus class", "Select a registered bus class from the setup table."));
      if (existing.Any(bus => bus.PartnerId == partnerId && string.Equals(bus.FleetNumber, row.Bus.FleetNumber, StringComparison.OrdinalIgnoreCase)))
        errors.Add(new(row.Row, "Fleet number", "A bus with this fleet number already exists for this partner."));
      if (existing.Any(bus => string.Equals(bus.PlateNumber, row.Bus.PlateNumber, StringComparison.OrdinalIgnoreCase)))
        errors.Add(new(row.Row, "Plate number", "A bus with this plate number already exists."));
    }
    if (errors.Count != 0) return new(0, errors);
    foreach (var row in rows)
    {
      var command = row.Bus;
      var bus = new Bus { PartnerId = partner.Id, FleetNumber = command.FleetNumber, PlateNumber = command.PlateNumber,
        BusClass = classByName[command.BusClass], CodeDecodeBusClass = classByName[command.BusClass].Code, SeatCapacity = command.SeatCapacity };
      db.Buses.Add(bus);
      db.UploadRecords.Add(new UploadRecord { UploadId = uploadId, BusId = bus.Id });
      CatalogAudit.Record(db, bus, userId, "Imported", uploadId: uploadId);
    }
    await PartnerCatalogActivityOutbox.QueueImportAsync(db, partner.Id, userId, "buses.imported", rows.Count, uploadId,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerBusConflictException(); }
    if (changes is not null) await changes.PublishAsync(["buses"], userId, partner.Id, cancellationToken);
    return new(rows.Count, []);
  }
}
