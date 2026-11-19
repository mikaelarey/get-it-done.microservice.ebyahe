using System.Data;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Messaging;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed partial class PartnerScheduledTripRepository
{
  public async Task<PartnerScheduledTripImportResult?> ImportAsync(string userId, Guid partnerId,
    IReadOnlyList<PartnerScheduledTripImportRow> rows, Guid uploadId, CancellationToken ct)
  {
    if (rows.Count is < 1 or > 500 || rows.Any(row => row.Trip.PartnerId != partnerId)) throw new ArgumentException("Invalid scheduled trip import batch.");
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    // This is the same operator lock used by manual creation and updates.
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {partnerId}").SingleOrDefaultAsync(ct);
    var partner = await BusOperators(userId).SingleOrDefaultAsync(partner => partner.Id == partnerId, ct);
    if (partner is null) return null;
    if (!await db.Uploads.AnyAsync(upload => upload.Id == uploadId && upload.CreatedBy == userId && upload.UploadType == UploadTypeCodes.ScheduledTrips, ct))
      throw new ArgumentException("A matching upload log is required.", nameof(uploadId));
    var buses = await db.Buses.Where(bus => bus.PartnerId == partnerId && bus.IsActive).ToDictionaryAsync(bus => bus.Id, ct);
    var terminals = await db.BusTerminals.Where(terminal => terminal.PartnerId == partnerId && terminal.IsActive && terminal.Location.IsActive).Select(terminal => terminal.Id).ToArrayAsync(ct);
    var routes = await db.BusRoutes.Where(route => route.PartnerId == partnerId).ToDictionaryAsync(route => (route.OriginTerminalId, route.DestinationTerminalId), ct);
    var errors = new List<PartnerScheduledTripImportError>();
    var now = clock.GetUtcNow();
    foreach (var row in rows)
    {
      var trip = row.Trip;
      void Error(string field, string message) => errors.Add(new(row.Sheet, row.Row, field, message));
      if (trip.DepartureAtUtc <= now) Error("Departure time (Manila)", "Choose a future departure date and time in Manila time.");
      if (trip.DurationMinutes is < 1 or > 2880 || trip.DepartureAtUtc > DateTimeOffset.MaxValue.AddMinutes(-Math.Clamp(trip.DurationMinutes, 1, 2880))) Error("Duration (minutes)", "Enter a valid travel time from 1 to 2880 minutes.");
      if (trip.BaseFare is < 0.01m or > 1000000m || decimal.Round(trip.BaseFare, 2) != trip.BaseFare) Error("Fare (PHP)", "Enter a valid fare with at most two decimal places.");
      if (!buses.ContainsKey(trip.BusId)) Error("Bus", "Select an active bus belonging to this partner. Download a new template to refresh the choices.");
      if (!terminals.Contains(trip.OriginTerminalId)) Error("Origin terminal", "Select an active origin terminal belonging to this partner.");
      if (!terminals.Contains(trip.DestinationTerminalId) || trip.OriginTerminalId == trip.DestinationTerminalId) Error("Destination terminal", "Select a different active destination terminal belonging to this partner.");
      if (routes.TryGetValue((trip.OriginTerminalId, trip.DestinationTerminalId), out var route) && (!route.IsActive || route.DurationMinutes != trip.DurationMinutes))
        Error("Duration (minutes)", "This route is inactive or its travel time has changed. Download a new template for the current route details.");
    }
    if (errors.Count != 0) return new(0, errors, uploadId);
    var start = rows.Min(row => row.Trip.DepartureAtUtc); var end = rows.Max(row => row.Trip.DepartureAtUtc.AddMinutes(row.Trip.DurationMinutes));
    var busIds = rows.Select(row => row.Trip.BusId).Distinct().ToArray();
    var existing = await db.ScheduledTrips.Where(trip => busIds.Contains(trip.BusId) && trip.Status == "Scheduled"
      && trip.DepartureAtUtc < end && trip.DepartureAtUtc.AddMinutes(trip.BusRoute.DurationMinutes) > start)
      .Select(trip => new { trip.BusId, Departure = trip.DepartureAtUtc, Duration = trip.BusRoute.DurationMinutes }).ToArrayAsync(ct);
    var accepted = new List<PartnerScheduledTripImportRow>();
    var durations = new Dictionary<(Guid, Guid), int>();
    foreach (var row in rows)
    {
      var trip = row.Trip; var arrival = trip.DepartureAtUtc.AddMinutes(trip.DurationMinutes);
      if (existing.Any(other => other.BusId == trip.BusId && other.Departure < arrival && other.Departure.AddMinutes(other.Duration) > trip.DepartureAtUtc))
        errors.Add(new(row.Sheet, row.Row, "Bus", "This bus already has a scheduled trip overlapping this departure and travel time."));
      var overlap = accepted.FirstOrDefault(other => other.Trip.BusId == trip.BusId && other.Trip.DepartureAtUtc < arrival && other.Trip.DepartureAtUtc.AddMinutes(other.Trip.DurationMinutes) > trip.DepartureAtUtc);
      if (overlap is not null) errors.Add(new(row.Sheet, row.Row, "Bus", $"This bus overlaps a trip on sheet {overlap.Sheet}, row {overlap.Row}."));
      var key = (trip.OriginTerminalId, trip.DestinationTerminalId);
      if (durations.TryGetValue(key, out var minutes) && minutes != trip.DurationMinutes)
        errors.Add(new(row.Sheet, row.Row, "Duration (minutes)", "All rows with the same origin and destination must use the same travel time."));
      durations[key] = trip.DurationMinutes; accepted.Add(row);
    }
    if (errors.Count != 0) return new(0, errors, uploadId);
    foreach (var row in rows)
    {
      var command = row.Trip; var key = (command.OriginTerminalId, command.DestinationTerminalId);
      if (!routes.TryGetValue(key, out var route))
      {
        route = new BusRoute { PartnerId = partnerId, OriginTerminalId = command.OriginTerminalId, DestinationTerminalId = command.DestinationTerminalId, DurationMinutes = command.DurationMinutes };
        db.BusRoutes.Add(route); routes.Add(key, route);
      }
      var trip = new ScheduledTrip { PartnerId = partnerId, BusId = command.BusId, BusRoute = route, BusRouteId = route.Id,
        DepartureAtUtc = command.DepartureAtUtc.ToUniversalTime(), BaseFare = command.BaseFare, AvailableSeats = buses[command.BusId].SeatCapacity };
      db.ScheduledTrips.Add(trip);
      db.UploadRecords.Add(new UploadRecord { UploadId = uploadId, ScheduledTripId = trip.Id });
      CatalogAudit.Record(db, trip, userId, "Imported", uploadId: uploadId);
    }
    await PartnerCatalogActivityOutbox.QueueImportAsync(db, partnerId, userId, "scheduled-trips.imported", rows.Count, uploadId,
      kafka?.Value.NotificationTopic ?? ActivityOutbox.DefaultTopic, ct);
    await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    if (changes is not null) await changes.PublishAsync(["scheduled-trips"], userId, partnerId, ct);
    return new(rows.Count, [], uploadId);
  }
}
