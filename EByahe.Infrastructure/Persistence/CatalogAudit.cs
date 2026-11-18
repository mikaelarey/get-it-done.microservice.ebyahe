using System.Text.Json;
using EByahe.Domain.Models;

namespace EByahe.Infrastructure.Persistence;

internal static class CatalogAudit
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  public static string Snapshot(Bus bus) => JsonSerializer.Serialize(new { bus.Id, bus.PartnerId, bus.FleetNumber,
    bus.PlateNumber, bus.CodeDecodeBusClass, busClass = bus.BusClass.Decode, bus.SeatCapacity, bus.IsActive }, JsonOptions);

  public static string Snapshot(BusTerminal terminal) => JsonSerializer.Serialize(new { terminal.Id, terminal.PartnerId,
    terminal.Name, terminal.Address, terminal.LocationId, location = terminal.Location.Name,
    terminal.Location.CodeDecodeRegion, terminal.Location.CodeDecodeProvince,
    terminal.Location.CodeDecodeMunicipality, terminal.Location.CodeDecodeBarangay, terminal.IsActive }, JsonOptions);

  public static string Snapshot(ScheduledTrip trip) => JsonSerializer.Serialize(new { trip.Id, trip.PartnerId, trip.BusId,
    trip.BusRouteId, trip.DepartureAtUtc, trip.BaseFare, trip.AvailableSeats, trip.CodeDecodeScheduledTripStatus }, JsonOptions);

  public static void Record(EByaheDbContext db, Bus bus, string actor, string action, string? previous = null, Guid? uploadId = null) =>
    db.BusLogs.Add(new BusLog { BusId = bus.Id, UploadId = uploadId, CreatedBy = actor, Action = action,
      PreviousValue = previous, CurrentValue = Snapshot(bus) });

  public static void Record(EByaheDbContext db, BusTerminal terminal, string actor, string action, string? previous = null, Guid? uploadId = null) =>
    db.TerminalLogs.Add(new TerminalLog { TerminalId = terminal.Id, UploadId = uploadId, CreatedBy = actor, Action = action,
      PreviousValue = previous, CurrentValue = Snapshot(terminal) });

  public static void Record(EByaheDbContext db, ScheduledTrip trip, string actor, string action, string? previous = null, Guid? uploadId = null) =>
    db.ScheduledTripLogs.Add(new ScheduledTripLog { ScheduledTripId = trip.Id, UploadId = uploadId, CreatedBy = actor, Action = action,
      PreviousValue = previous, CurrentValue = Snapshot(trip) });
}
