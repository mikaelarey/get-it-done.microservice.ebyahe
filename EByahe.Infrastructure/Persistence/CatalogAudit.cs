using System.Text.Json;
using EByahe.Domain.Models;

namespace EByahe.Infrastructure.Persistence;

internal static class CatalogAudit
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  public static string Snapshot(Room room) => JsonSerializer.Serialize(new { room.Id, room.HotelId, room.Name, room.CodeDecodeRoomType, RoomType = room.RoomType.Decode,
    room.GuestCapacity, room.NightlyRate, room.Description, room.IsActive, room.IsDeleted, room.DeletedBy, room.DeletedAtUtc,
    room.CreatedBy, room.CreatedAtUtc, Images = room.Images.Where(image => !image.IsDeleted).OrderBy(image => image.SortOrder)
      .Select(image => new { image.Id, image.FileName, image.ContentType, image.SizeBytes, image.Sha256 }) }, JsonOptions);
  public static void Record(EByaheDbContext db, Room room, string actor, string action, string? previous = null) =>
    db.RoomLogs.Add(new RoomLog { RoomId = room.Id, CreatedBy = actor, Action = action, PreviousValue = previous, CurrentValue = Snapshot(room) });

  public static string Snapshot(Hotel hotel) => JsonSerializer.Serialize(new { hotel.Id, hotel.PartnerId, hotel.Name,
    hotel.Address, hotel.CodeDecodeRegion, hotel.CodeDecodeProvince, hotel.CodeDecodeMunicipality, hotel.CodeDecodeBarangay,
    hotel.ContactNumber, hotel.Email, hotel.Description, hotel.IsActive, hotel.IsDeleted, hotel.DeletedBy, hotel.DeletedAtUtc,
    hotel.CreatedBy, hotel.CreatedAtUtc }, JsonOptions);

  public static void Record(EByaheDbContext db, Hotel hotel, string actor, string action, string? previous = null) =>
    db.HotelLogs.Add(new HotelLog { HotelId = hotel.Id, CreatedBy = actor, Action = action,
      PreviousValue = previous, CurrentValue = Snapshot(hotel) });

  public static string Snapshot(Bus bus) => JsonSerializer.Serialize(new { bus.Id, bus.PartnerId, bus.FleetNumber,
    bus.PlateNumber, bus.CodeDecodeBusClass, busClass = bus.BusClass.Decode, bus.SeatCapacity, bus.IsActive }, JsonOptions);

  public static string Snapshot(BusTerminal terminal) => JsonSerializer.Serialize(new { terminal.Id, terminal.PartnerId,
    terminal.Name, terminal.Address, terminal.LocationId, location = terminal.Location.Name,
    terminal.Location.CodeDecodeRegion, terminal.Location.CodeDecodeProvince,
    terminal.Location.CodeDecodeMunicipality, terminal.Location.CodeDecodeBarangay, terminal.IsActive }, JsonOptions);

  public static string Snapshot(ScheduledTrip trip) => JsonSerializer.Serialize(new { trip.Id, trip.PartnerId, trip.BusId,
    trip.BusRouteId, trip.DepartureAtUtc, trip.DurationMinutes, trip.BaseFare, trip.AvailableSeats, trip.CodeDecodeScheduledTripStatus }, JsonOptions);

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
