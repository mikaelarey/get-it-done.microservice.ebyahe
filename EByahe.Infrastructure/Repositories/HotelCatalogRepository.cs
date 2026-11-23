using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class HotelCatalogRepository(EByaheDbContext db) : IHotelCatalogRepository
{
  // Public discovery only includes active catalog records belonging to approved hotel companies.
  // Hotel bookings currently snapshot room names, without a room inventory relation, so this
  // catalog describes rooms and prices rather than claiming availability for particular dates.
  private IQueryable<Room> Published() => db.Rooms.AsNoTracking().Where(room => room.IsActive && !room.IsDeleted
    && room.Hotel.IsActive && !room.Hotel.IsDeleted && room.Hotel.Partner.Status == "Approved"
    && db.PartnerApplications.Any(application => application.Id == room.Hotel.Partner.PartnerApplicationId
      && application.Status == "Approved" && application.BusinessCategory == "hotel"));

  private IQueryable<Room> Matching(int guests, int? roomType) => Published().Where(room => room.GuestCapacity >= guests
    && (!roomType.HasValue || room.CodeDecodeRoomType == roomType.Value));

  private IQueryable<HotelCatalogEntry> Entries(IQueryable<Room> rooms, IQueryable<Hotel>? hotels = null) => (hotels ?? db.Hotels.AsNoTracking())
    .Where(hotel => rooms.Any(room => room.HotelId == hotel.Id))
    .Select(hotel => new HotelCatalogEntry(hotel.Id, hotel.Name, hotel.Address, hotel.Municipality.Decode, hotel.Province.Decode,
      hotel.Description, hotel.ContactNumber, hotel.Email, rooms.Count(room => room.HotelId == hotel.Id),
      rooms.Where(room => room.HotelId == hotel.Id).Min(room => room.NightlyRate),
      rooms.Where(room => room.HotelId == hotel.Id).SelectMany(room => room.Images).Where(image => !image.IsDeleted)
        .OrderBy(image => image.Room.NightlyRate).ThenBy(image => image.RoomId).ThenBy(image => image.SortOrder).ThenBy(image => image.Id)
        .Select(image => new HotelCatalogPhoto(image.RoomId, image.Id)).FirstOrDefault()));

  public async Task<PartnerRecordsPage<HotelCatalogEntry>> SearchAsync(string? search, int guests, int? roomType, string sort, int page, CancellationToken ct)
  {
    var rooms = Matching(guests, roomType);
    foreach (var term in PartnerKeywordSearch.Tokens(search))
      rooms = rooms.Where(room => EF.Functions.Collate(room.Hotel.Name + " " + room.Hotel.Address + " "
        + room.Hotel.Municipality.Decode + " " + room.Hotel.Province.Decode + " " + room.Hotel.Barangay.Decode
        + " " + room.Hotel.Region.Decode, PartnerKeywordSearch.Collation).Contains(term));
    var query = db.Hotels.AsNoTracking().Where(hotel => rooms.Any(room => room.HotelId == hotel.Id));
    var count = await query.CountAsync(ct);
    var ordered = sort switch {
      "price-asc" => query.OrderBy(hotel => rooms.Where(room => room.HotelId == hotel.Id).Min(room => room.NightlyRate)).ThenBy(hotel => hotel.Name).ThenBy(hotel => hotel.Id),
      "price-desc" => query.OrderByDescending(hotel => rooms.Where(room => room.HotelId == hotel.Id).Min(room => room.NightlyRate)).ThenBy(hotel => hotel.Name).ThenBy(hotel => hotel.Id),
      _ => query.OrderBy(hotel => hotel.Name).ThenBy(hotel => hotel.Id)
    };
    return new(await Entries(rooms, ordered.Skip((page - 1) * 12).Take(12)).ToArrayAsync(ct), count, page, 12);
  }

  public async Task<IReadOnlyList<RoomTypeOption>> TypesAsync(CancellationToken ct) =>
    await Published().Select(room => room.RoomType).Distinct().OrderBy(type => type.Code)
      .Select(type => new RoomTypeOption(type.Code, type.Decode)).ToArrayAsync(ct);

  public async Task<HotelCatalogDetails?> GetAsync(Guid id, int guests, int? roomType, CancellationToken ct)
  {
    var rooms = Matching(guests, roomType).Where(room => room.HotelId == id);
    var hotel = await Entries(rooms).SingleOrDefaultAsync(ct);
    if (hotel is null) return null;
    var items = await rooms.OrderBy(room => room.NightlyRate).ThenBy(room => room.Name).ThenBy(room => room.Id).Take(100)
      .Select(room => new HotelCatalogRoom(room.Id, room.Name, room.RoomType.Decode, room.GuestCapacity, room.NightlyRate,
        room.Description, room.Images.Where(image => !image.IsDeleted).OrderBy(image => image.SortOrder).ThenBy(image => image.Id)
          .Select(image => new HotelCatalogPhoto(room.Id, image.Id)).ToArray())).ToArrayAsync(ct);
    return new(hotel, items);
  }

  public Task<RoomImage?> GetImageAsync(Guid roomId, Guid imageId, CancellationToken ct) => db.RoomImages.AsNoTracking()
    .Where(image => image.RoomId == roomId && image.Id == imageId && !image.IsDeleted
      && Published().Any(room => room.Id == image.RoomId)).SingleOrDefaultAsync(ct);
}
