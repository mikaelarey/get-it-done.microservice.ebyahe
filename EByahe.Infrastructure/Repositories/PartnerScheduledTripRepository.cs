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

public sealed partial class PartnerScheduledTripRepository(EByaheDbContext db, TimeProvider clock, IOptions<KafkaOptions>? kafka = null, IRecordChangePublisher? changes = null) : IPartnerScheduledTripRepository
{
  private IQueryable<Partner> BusOperators(string userId) => db.Partners.Where(partner => partner.Status == "Approved"
    && db.PartnerUsers.Any(member => member.PartnerId == partner.Id && member.IsActive && member.RefUser.ExternalUserId == userId)
    && db.PartnerApplications.Any(application => application.Id == partner.PartnerApplicationId
      && application.Status == "Approved" && application.BusinessCategory == "bus-operator"));

  public async Task<PartnerTripOptions> OptionsAsync(string userId, CancellationToken cancellationToken)
  {
    var ids = BusOperators(userId).Select(partner => partner.Id);
    var workspaces = await BusOperators(userId).AsNoTracking().OrderBy(partner => partner.DisplayName).ThenBy(partner => partner.Id)
      .Select(partner => new PartnerTripWorkspace(partner.Id, partner.DisplayName)).ToArrayAsync(cancellationToken);
    var buses = await db.Buses.AsNoTracking().Where(bus => ids.Contains(bus.PartnerId) && bus.IsActive)
      .OrderBy(bus => bus.FleetNumber).ThenBy(bus => bus.Id)
      .Select(bus => new PartnerTripBus(bus.Id, bus.PartnerId, bus.FleetNumber, bus.PlateNumber, bus.BusClass.Decode, bus.SeatCapacity)).ToArrayAsync(cancellationToken);
    var terminals = await db.BusTerminals.AsNoTracking().Where(terminal => ids.Contains(terminal.PartnerId) && terminal.IsActive && terminal.Location.IsActive)
      .OrderBy(terminal => terminal.Location.Name).ThenBy(terminal => terminal.Name).ThenBy(terminal => terminal.Id)
      .Select(terminal => new PartnerTripTerminal(terminal.Id, terminal.PartnerId, terminal.Name, terminal.Location.Name)).ToArrayAsync(cancellationToken);
    var routes = await db.BusRoutes.AsNoTracking().Where(route => ids.Contains(route.PartnerId) && route.IsActive
      && route.OriginTerminal.IsActive && route.DestinationTerminal.IsActive && route.OriginTerminal.Location.IsActive && route.DestinationTerminal.Location.IsActive)
      .OrderBy(route => route.Id).Select(route => new PartnerTripRoute(route.Id, route.PartnerId, route.OriginTerminalId, route.DestinationTerminalId, route.DurationMinutes))
      .ToArrayAsync(cancellationToken);
    return new(workspaces, buses, terminals, routes);
  }

  private IQueryable<ScheduledTrip> VisibleTrips(string userId) => db.ScheduledTrips
    .Where(trip => BusOperators(userId).Select(partner => partner.Id).Contains(trip.PartnerId));

  private async Task<string?> UpdateRestrictionAsync(ScheduledTrip trip, CancellationToken ct)
  {
    if (trip.Status != "Scheduled") return "Only scheduled trips can be updated.";
    if (trip.DepartureAtUtc <= clock.GetUtcNow()) return "This trip has already departed and cannot be updated.";
    if (await db.BusBookings.AnyAsync(booking => booking.ScheduledTripId == trip.Id && booking.Status != "Cancelled", ct))
      return "This trip has active bookings and cannot be updated. Existing booking and payment details must be preserved.";
    if (trip.AvailableSeats != trip.Bus.SeatCapacity) return "This trip has allocated or unconfigured seats and cannot be updated.";
    return null;
  }

  public async Task<PartnerScheduledTripDetails?> DetailsAsync(string userId, Guid id, CancellationToken ct)
  {
    var trip = await VisibleTrips(userId).AsNoTracking().Include(row => row.Partner).Include(row => row.Bus).ThenInclude(bus => bus.BusClass)
      .Include(row => row.BusRoute).ThenInclude(route => route.OriginTerminal).ThenInclude(terminal => terminal.Location)
      .Include(row => row.BusRoute).ThenInclude(route => route.DestinationTerminal).ThenInclude(terminal => terminal.Location)
      .SingleOrDefaultAsync(row => row.Id == id, ct);
    if (trip is null) return null;
    var restriction = await UpdateRestrictionAsync(trip, ct);
    return new(trip.Id, trip.PartnerId, trip.Partner.DisplayName, trip.BusId, trip.Bus.FleetNumber, trip.Bus.PlateNumber,
      trip.Bus.BusClass.Decode, trip.Bus.SeatCapacity, trip.BusRoute.OriginTerminalId, trip.BusRoute.OriginTerminal.Name,
      trip.BusRoute.OriginTerminal.Location.Name, trip.BusRoute.DestinationTerminalId, trip.BusRoute.DestinationTerminal.Name,
      trip.BusRoute.DestinationTerminal.Location.Name, trip.DepartureAtUtc, trip.BusRoute.DurationMinutes, trip.AvailableSeats,
      trip.BaseFare, trip.Status, restriction is null, restriction);
  }

  public Task<PartnerPortalTrip?> AddAsync(string userId, AddPartnerScheduledTrip command, CancellationToken ct) => SaveAsync(userId, null, command, ct);

  public async Task<PartnerPortalTrip?> UpdateAsync(string userId, Guid id, UpdatePartnerScheduledTrip command, CancellationToken ct)
  {
    var partnerId = await VisibleTrips(userId).AsNoTracking().Where(trip => trip.Id == id).Select(trip => (Guid?)trip.PartnerId).SingleOrDefaultAsync(ct);
    return partnerId is null ? null : await SaveAsync(userId, id, new(partnerId.Value, command.BusId, command.OriginTerminalId,
      command.DestinationTerminalId, command.DepartureAtUtc, command.DurationMinutes, command.BaseFare), ct);
  }

  private async Task<PartnerPortalTrip?> SaveAsync(string userId, Guid? id, AddPartnerScheduledTrip command, CancellationToken cancellationToken)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    // Serialize route creation and inventory checks for this operator, including concurrent requests.
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {command.PartnerId}")
      .SingleOrDefaultAsync(cancellationToken);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == command.PartnerId, cancellationToken);
    if (partner is null) return null;
    ScheduledTrip? existing = null;
    string? previous = null;
    if (id is not null)
    {
      existing = await db.ScheduledTrips.FromSqlInterpolated($"SELECT * FROM ScheduledTrips WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id.Value}")
        .Include(trip => trip.Bus).SingleOrDefaultAsync(trip => trip.PartnerId == partner.Id, cancellationToken);
      if (existing is null) return null;
      var restriction = await UpdateRestrictionAsync(existing, cancellationToken);
      if (restriction is not null) throw new PartnerScheduledTripConflictException(restriction);
      previous = CatalogAudit.Snapshot(existing);
    }
    var departure = command.DepartureAtUtc.ToUniversalTime();
    if (departure <= clock.GetUtcNow()) throw new PartnerScheduledTripValidationException("Choose a future departure date and time.");
    var bus = await db.Buses.Include(bus => bus.BusClass).SingleOrDefaultAsync(bus => bus.Id == command.BusId && bus.PartnerId == partner.Id && bus.IsActive, cancellationToken);
    if (bus is null) throw new PartnerScheduledTripValidationException("Select an active bus belonging to this partner.");
    var terminals = await db.BusTerminals.Include(terminal => terminal.Location).Where(terminal => terminal.PartnerId == partner.Id
      && terminal.IsActive && terminal.Location.IsActive && (terminal.Id == command.OriginTerminalId || terminal.Id == command.DestinationTerminalId))
      .ToArrayAsync(cancellationToken);
    var origin = terminals.SingleOrDefault(terminal => terminal.Id == command.OriginTerminalId);
    var destination = terminals.SingleOrDefault(terminal => terminal.Id == command.DestinationTerminalId);
    if (origin is null || destination is null || origin.Id == destination.Id)
      throw new PartnerScheduledTripValidationException("Select two different active terminals belonging to this partner.");
    var route = await db.BusRoutes.SingleOrDefaultAsync(route => route.PartnerId == partner.Id
      && route.OriginTerminalId == origin.Id && route.DestinationTerminalId == destination.Id, cancellationToken);
    if (route is not null && (!route.IsActive || route.DurationMinutes != command.DurationMinutes))
      throw new PartnerScheduledTripValidationException("This route is inactive or its travel time has changed. Reopen the form to load its current details.");
    var arrival = departure.AddMinutes(command.DurationMinutes);
    if (await db.ScheduledTrips.AnyAsync(trip => trip.Id != id && trip.BusId == bus.Id && trip.Status == "Scheduled"
      && trip.DepartureAtUtc < arrival && trip.DepartureAtUtc.AddMinutes(trip.BusRoute.DurationMinutes) > departure, cancellationToken))
      throw new PartnerScheduledTripConflictException();
    if (route is null)
    {
      route = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = command.DurationMinutes };
      db.BusRoutes.Add(route);
    }
    var trip = existing ?? new ScheduledTrip { PartnerId = partner.Id };
    trip.BusId = bus.Id; trip.BusRoute = route; trip.BusRouteId = route.Id; trip.DepartureAtUtc = departure;
    trip.BaseFare = command.BaseFare; trip.AvailableSeats = bus.SeatCapacity;
    if (existing is null) db.ScheduledTrips.Add(trip);
    CatalogAudit.Record(db, trip, userId, existing is null ? "Created" : "Updated", previous);
    await PartnerCatalogActivityOutbox.QueueAsync(db, partner.Id, userId, existing is null ? "scheduled-trip.created" : "scheduled-trip.updated", trip.Id,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, cancellationToken);
    try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerScheduledTripConflictException(); }
    if (changes is not null) await changes.PublishAsync(["scheduled-trips"], userId, partner.Id, cancellationToken);
    return new(trip.Id, origin.Location.Name, destination.Location.Name, partner.DisplayName, bus.PlateNumber,
      bus.BusClass.Decode, trip.DepartureAtUtc, trip.AvailableSeats, trip.BaseFare, trip.Status);
  }
}
