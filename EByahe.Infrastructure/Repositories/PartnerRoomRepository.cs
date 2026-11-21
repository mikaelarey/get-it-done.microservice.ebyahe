using System.Data;
using System.Linq.Expressions;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerRoomRepository(EByaheDbContext db) : IPartnerRoomRepository
{
  private IQueryable<Guid> Partners(string userId) => db.Partners.Where(partner => partner.Status == "Approved"
    && db.PartnerUsers.Any(member => member.PartnerId == partner.Id && member.IsActive && member.RefUser.ExternalUserId == userId)
    && db.PartnerApplications.Any(application => application.Id == partner.PartnerApplicationId
      && application.Status == "Approved" && application.BusinessCategory == "hotel")).Select(partner => partner.Id);
  private IQueryable<Room> Visible(string userId) => db.Rooms.AsNoTracking().Where(room => !room.IsDeleted
    && !room.Hotel.IsDeleted && Partners(userId).Contains(room.Hotel.PartnerId));
  private static readonly Expression<Func<Room, PartnerRoom>> Project = room => new(room.Id, room.HotelId, room.Hotel.Name,
    room.Hotel.PartnerId, room.Hotel.Partner.DisplayName, room.Name, room.RoomType.Decode, room.GuestCapacity, room.NightlyRate,
    room.Description, room.IsActive, room.Hotel.IsActive, room.Images.Where(image => !image.IsDeleted).OrderBy(image => image.SortOrder)
      .Select(image => new PartnerRoomImage(image.Id, image.FileName, image.ContentType, image.SizeBytes)).ToArray());

  public async Task<PartnerRecordsPage<PartnerRoom>> ListAsync(string userId, int page, int pageSize, string? search, Guid? hotelId, CancellationToken ct)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var query = Visible(userId);
    if (hotelId.HasValue) query = query.Where(room => room.HotelId == hotelId.Value);
    foreach (var term in PartnerKeywordSearch.Tokens(search))
      query = query.Where(room => EF.Functions.Collate(room.Name + " " + room.RoomType.Decode + " " + room.Hotel.Name + " "
        + (room.Description ?? ""), PartnerKeywordSearch.Collation).Contains(term));
    var count = await query.CountAsync(ct);
    var items = await query.OrderBy(room => room.Hotel.Name).ThenBy(room => room.Name).ThenBy(room => room.Id)
      .Skip((page - 1) * pageSize).Take(pageSize).Select(Project).ToArrayAsync(ct);
    return new(items, count, page, pageSize);
  }
  public async Task<IReadOnlyList<PartnerRoomHotel>> HotelsAsync(string userId, CancellationToken ct) =>
    await db.Hotels.AsNoTracking().Where(hotel => !hotel.IsDeleted && Partners(userId).Contains(hotel.PartnerId))
      .OrderBy(hotel => hotel.Name).ThenBy(hotel => hotel.Id).Select(hotel => new PartnerRoomHotel(hotel.Id, hotel.Name, hotel.Partner.DisplayName, hotel.IsActive)).ToArrayAsync(ct);
  public Task<PartnerRoom?> GetAsync(string userId, Guid id, CancellationToken ct) =>
    Visible(userId).Where(room => room.Id == id).Select(Project).SingleOrDefaultAsync(ct);
  public Task<RoomImage?> GetImageAsync(string userId, Guid roomId, Guid imageId, CancellationToken ct) =>
    Visible(userId).Where(room => room.Id == roomId).SelectMany(room => room.Images)
      .SingleOrDefaultAsync(image => image.Id == imageId && !image.IsDeleted, ct);
  public Task<bool> AnyImageExistsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
    db.RoomImages.AsNoTracking().AnyAsync(image => ids.Contains(image.Id), ct);
  public async Task<IReadOnlyList<RoomTypeOption>> TypesAsync(CancellationToken ct) => await db.CodeDecodeRoomTypes.AsNoTracking()
    .OrderBy(type => type.Code).Select(type => new RoomTypeOption(type.Code, type.Decode)).ToArrayAsync(ct);
  private async Task<CodeDecodeRoomType> ResolveType(string decode, CancellationToken ct) =>
    await db.CodeDecodeRoomTypes.SingleOrDefaultAsync(type => type.Decode == decode, ct) ?? throw new PartnerRoomTypeException();

  // Lock the parent before the room in every mutation, including hotel deletion races.
  private Task<Hotel?> LockHotel(string userId, Guid id, CancellationToken ct) => db.Hotels
    .FromSqlInterpolated($"SELECT * FROM [Hotels] WITH (UPDLOCK) WHERE [Id] = {id}")
    .SingleOrDefaultAsync(hotel => !hotel.IsDeleted && Partners(userId).Contains(hotel.PartnerId), ct);
  private IQueryable<Room> LockRoom(Guid id) => db.Rooms.FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK) WHERE [Id] = {id}");

  public async Task<PartnerRoom?> AddAsync(string userId, SavePartnerRoom command, CancellationToken ct)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var hotel = await LockHotel(userId, command.HotelId, ct);
    if (hotel is null) return null;
    if (!hotel.IsActive) throw new PartnerRoomHotelInactiveException();
    var roomType = await ResolveType(command.RoomType, ct);
    var room = new Room { HotelId = hotel.Id, Name = command.Name, RoomType = roomType, CodeDecodeRoomType = roomType.Code, GuestCapacity = command.GuestCapacity,
      NightlyRate = command.NightlyRate, Description = command.Description, CreatedBy = userId };
    UpdateImages(room, command, userId);
    db.Rooms.Add(room); CatalogAudit.Record(db, room, userId, "Created");
    await Save(ct); var result = await GetAsync(userId, room.Id, ct);
    await transaction.CommitAsync(CancellationToken.None); return result;
  }
  public async Task<PartnerRoom?> UpdateAsync(string userId, Guid id, SavePartnerRoom command, bool isActive, CancellationToken ct)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    if (await LockHotel(userId, command.HotelId, ct) is null) return null;
    var room = await LockRoom(id).Include(row => row.Images).Include(row => row.RoomType).SingleOrDefaultAsync(row => !row.IsDeleted && row.HotelId == command.HotelId, ct);
    if (room is null) return null;
    var previous = CatalogAudit.Snapshot(room);
    var roomType = await ResolveType(command.RoomType, ct);
    room.Name = command.Name; room.RoomType = roomType; room.CodeDecodeRoomType = roomType.Code; room.GuestCapacity = command.GuestCapacity;
    room.NightlyRate = command.NightlyRate; room.Description = command.Description; room.IsActive = isActive;
    UpdateImages(room, command, userId);
    CatalogAudit.Record(db, room, userId, "Updated", previous);
    await Save(ct); var result = await GetAsync(userId, id, ct);
    await transaction.CommitAsync(CancellationToken.None); return result;
  }
  public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct)
  {
    var hotelId = await db.Rooms.AsNoTracking().Where(room => room.Id == id && !room.Hotel.IsDeleted
      && Partners(userId).Contains(room.Hotel.PartnerId)).Select(room => (Guid?)room.HotelId).SingleOrDefaultAsync(ct);
    if (hotelId is null) return false;
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    if (await LockHotel(userId, hotelId.Value, ct) is null) return false;
    var room = await LockRoom(id).Include(row => row.Images).Include(row => row.RoomType).SingleOrDefaultAsync(row => row.HotelId == hotelId.Value, ct);
    if (room is null) return false;
    if (!room.IsDeleted) {
      var previous = CatalogAudit.Snapshot(room);
      room.IsDeleted = true; room.DeletedBy = userId; room.DeletedAtUtc = DateTimeOffset.UtcNow;
      CatalogAudit.Record(db, room, userId, "Deleted", previous); await db.SaveChangesAsync(ct);
    }
    await transaction.CommitAsync(ct); return true;
  }
  private async Task Save(CancellationToken ct)
  {
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerRoomConflictException(); }
  }
  private static void UpdateImages(Room room, SavePartnerRoom command, string userId)
  {
    var existing = room.Images.Where(image => !image.IsDeleted).ToArray();
    var retained = command.RetainedImageIds ?? existing.Select(image => image.Id).ToArray();
    var added = command.NewImages ?? [];
    if (retained.Distinct().Count() != retained.Count || retained.Any(id => !existing.Any(image => image.Id == id))
      || retained.Count + added.Count > 8) throw new PartnerRoomImagesException();
    foreach (var image in existing.Where(image => !retained.Contains(image.Id))) {
      image.IsDeleted = true; image.DeletedBy = userId; image.DeletedAtUtc = DateTimeOffset.UtcNow;
    }
    var order = 0;
    foreach (var id in retained) existing.Single(image => image.Id == id).SortOrder = order++;
    foreach (var image in added) { image.SortOrder = order++; image.CreatedBy = userId; room.Images.Add(image); }
  }
}
