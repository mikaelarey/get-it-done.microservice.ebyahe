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

public sealed class PartnerTerminalRepository(EByaheDbContext db, IOptions<KafkaOptions>? kafka = null, IRecordChangePublisher? changes = null) : IPartnerTerminalRepository
{
  private IQueryable<Partner> BusOperators(string userId) => db.Partners.Where(partner => partner.Status == "Approved"
    && db.PartnerUsers.Any(member => member.PartnerId == partner.Id && member.IsActive && member.RefUser.ExternalUserId == userId)
    && db.PartnerApplications.Any(application => application.Id == partner.PartnerApplicationId
      && application.Status == "Approved" && application.BusinessCategory == "bus-operator"));

  public async Task<IReadOnlyList<PartnerTerminalWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken) =>
    await BusOperators(userId).AsNoTracking().OrderBy(partner => partner.DisplayName).ThenBy(partner => partner.Id)
      .Select(partner => new PartnerTerminalWorkspace(partner.Id, partner.DisplayName)).ToArrayAsync(cancellationToken);

  public async Task<IReadOnlyList<PartnerTerminalLocation>> LocationsAsync(string userId, CancellationToken cancellationToken)
  {
    if (!await BusOperators(userId).AnyAsync(cancellationToken)) return [];
    return await db.Locations.AsNoTracking().Where(location => location.IsActive).OrderBy(location => location.Name).ThenBy(location => location.Id)
      .Select(location => new PartnerTerminalLocation(location.Id, location.Name, location.CountryCode)).ToArrayAsync(cancellationToken);
  }

  public async Task<PartnerPortalTerminal?> AddAsync(string userId, AddPartnerTerminal command, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == command.PartnerId, cancellationToken);
    if (partner is null) return null;
    var location = await db.Locations.SingleOrDefaultAsync(location => location.Id == command.LocationId && location.IsActive, cancellationToken);
    if (location is null) throw new PartnerTerminalLocationUnavailableException();
    var terminal = new BusTerminal { PartnerId = partner.Id, LocationId = location.Id, Name = command.Name, Address = command.Address };
    db.BusTerminals.Add(terminal);
    await PartnerCatalogActivityOutbox.QueueAsync(db, partner.Id, userId, "terminal.created", terminal.Id,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerTerminalConflictException(); }
    if (changes is not null) await changes.PublishAsync(["terminals"], userId, partner.Id, cancellationToken);
    return new(terminal.Id, terminal.Name, location.Name, terminal.Address, partner.DisplayName, terminal.IsActive);
  }
}
