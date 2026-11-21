using System.Data;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed partial class PartnerScheduledTripRepository
{
  private IQueryable<BusRoute> VisibleTravelTimes(string userId) => db.BusRoutes.AsNoTracking()
    .Where(route => BusOperators(userId).Select(partner => partner.Id).Contains(route.PartnerId));

  public async Task<PartnerRecordsPage<PartnerTravelTime>> TravelTimesAsync(string userId, int page, int pageSize, string? search, CancellationToken ct,
    Guid? originTerminalId = null, Guid? destinationTerminalId = null)
  {
    var routes = VisibleTravelTimes(userId);
    if (originTerminalId.HasValue) routes = routes.Where(route => route.OriginTerminalId == originTerminalId.Value);
    if (destinationTerminalId.HasValue) routes = routes.Where(route => route.DestinationTerminalId == destinationTerminalId.Value);
    foreach (var term in PartnerKeywordSearch.Tokens(search))
      routes = routes.Where(route => EF.Functions.Collate(route.OriginTerminal.Name + " " + route.OriginTerminal.Location.Name + " "
        + route.DestinationTerminal.Name + " " + route.DestinationTerminal.Location.Name + " " + (route.DurationMinutes == null ? "Not configured" : route.DurationMinutes.ToString()), PartnerKeywordSearch.Collation).Contains(term));
    var count = await routes.CountAsync(ct);
    var items = await routes.OrderBy(route => route.OriginTerminal.Name).ThenBy(route => route.DestinationTerminal.Name).ThenBy(route => route.Id)
      .Skip((page - 1) * pageSize).Take(pageSize)
      .Select(route => new PartnerTravelTime(route.Id, route.PartnerId, route.Partner.DisplayName, route.OriginTerminalId,
        route.OriginTerminal.Name + " (" + route.OriginTerminal.Location.Name + ")", route.DestinationTerminalId,
        route.DestinationTerminal.Name + " (" + route.DestinationTerminal.Location.Name + ")", route.DurationMinutes, route.IsActive)).ToArrayAsync(ct);
    return new(items, count, page, pageSize);
  }

  public async Task<IReadOnlyList<PartnerTripFilterTerminal>> TravelTimeFilterTerminalsAsync(string userId, CancellationToken ct)
  {
    var partners = BusOperators(userId).Select(partner => partner.Id);
    // Inactive terminals remain available because their existing travel-time rows are still visible.
    return await db.BusTerminals.AsNoTracking().Where(terminal => partners.Contains(terminal.PartnerId))
      .OrderBy(terminal => terminal.Name).ThenBy(terminal => terminal.Location.Name).ThenBy(terminal => terminal.Id)
      .Select(terminal => new PartnerTripFilterTerminal(terminal.Id, terminal.Name, terminal.Location.Name, terminal.Partner.DisplayName))
      .ToArrayAsync(ct);
  }

  public async Task<PartnerTravelTimeOptions> TravelTimeOptionsAsync(string userId, CancellationToken ct)
  {
    var partners = BusOperators(userId);
    var workspaces = await partners.AsNoTracking().OrderBy(partner => partner.DisplayName).ThenBy(partner => partner.Id)
      .Select(partner => new PartnerTripWorkspace(partner.Id, partner.DisplayName)).ToArrayAsync(ct);
    var terminals = await db.BusTerminals.AsNoTracking().Where(terminal => partners.Select(partner => partner.Id).Contains(terminal.PartnerId)
      && terminal.IsActive && terminal.Location.IsActive).OrderBy(terminal => terminal.Name).ThenBy(terminal => terminal.Id)
      .Select(terminal => new PartnerTripTerminal(terminal.Id, terminal.PartnerId, terminal.Name, terminal.Location.Name)).ToArrayAsync(ct);
    return new(workspaces, terminals);
  }

  public Task<PartnerTravelTime?> AddTravelTimeAsync(string userId, AddPartnerTravelTime command, CancellationToken ct) =>
    SaveTravelTimeAsync(userId, null, command, null, ct);

  public async Task<PartnerTravelTime?> UpdateTravelTimeAsync(string userId, Guid id, UpdatePartnerTravelTime command, CancellationToken ct)
  {
    var route = await VisibleTravelTimes(userId).SingleOrDefaultAsync(route => route.Id == id, ct);
    return route is null ? null : await SaveTravelTimeAsync(userId, id,
      new(route.PartnerId, route.OriginTerminalId, route.DestinationTerminalId, command.DurationMinutes), command.IsActive, ct);
  }

  private async Task<PartnerTravelTime?> SaveTravelTimeAsync(string userId, Guid? id, AddPartnerTravelTime command, bool? active, CancellationToken ct)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {command.PartnerId}").SingleOrDefaultAsync(ct);
    var partner = await BusOperators(userId).AsNoTracking().SingleOrDefaultAsync(partner => partner.Id == command.PartnerId, ct);
    if (partner is null) return null;
    if (command.DurationMinutes is < 1 or > 2880 || command.OriginTerminalId == command.DestinationTerminalId)
      throw new PartnerScheduledTripValidationException("Select different origin and destination terminals and enter travel time from 1 to 2880 minutes.");
    var terminals = await db.BusTerminals.Include(terminal => terminal.Location).Where(terminal => terminal.PartnerId == command.PartnerId
      && (terminal.Id == command.OriginTerminalId || terminal.Id == command.DestinationTerminalId)).ToArrayAsync(ct);
    if (terminals.Length != 2 || ((id is null || active == true) && terminals.Any(terminal => !terminal.IsActive || !terminal.Location.IsActive)))
      throw new PartnerScheduledTripValidationException("Select two active terminals belonging to this partner.");
    var route = await db.BusRoutes.SingleOrDefaultAsync(route => route.PartnerId == command.PartnerId
      && route.OriginTerminalId == command.OriginTerminalId && route.DestinationTerminalId == command.DestinationTerminalId, ct);
    if (id is null && route is not null)
      throw new PartnerScheduledTripConflictException("Travel time already exists for this origin and destination. Update the existing entry.");
    if (id is not null && route?.Id != id) return null;
    if (route is null) {
      route = new BusRoute { PartnerId = command.PartnerId, OriginTerminalId = command.OriginTerminalId, DestinationTerminalId = command.DestinationTerminalId };
      db.BusRoutes.Add(route);
    } else if (route.DurationMinutes != command.DurationMinutes) {
      // Preserve timings for historical and already-published trips, including older writers without snapshots.
      await db.ScheduledTrips.Where(trip => trip.BusRouteId == route.Id && trip.DurationMinutes == null)
        .ExecuteUpdateAsync(update => update.SetProperty(trip => trip.DurationMinutes, route.DurationMinutes), ct);
    }
    route.DurationMinutes = command.DurationMinutes; route.IsActive = active ?? true;
    await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    if (changes is not null) await changes.PublishAsync(["travel-times", "scheduled-trips"], userId, partner.Id, ct);
    var origin = terminals.Single(terminal => terminal.Id == route.OriginTerminalId); var destination = terminals.Single(terminal => terminal.Id == route.DestinationTerminalId);
    return new(route.Id, partner.Id, partner.DisplayName, origin.Id, origin.Name + " (" + origin.Location.Name + ")",
      destination.Id, destination.Name + " (" + destination.Location.Name + ")", route.DurationMinutes, route.IsActive);
  }
}
