namespace EByahe.Domain.DataModels;

public sealed record HotelCatalogPhoto(Guid RoomId, Guid ImageId);
public sealed record HotelCatalogEntry(Guid Id, string Name, string Address, string Municipality, string Province,
  string? Description, string ContactNumber, string Email, int RoomCount, decimal FromNightlyRate, HotelCatalogPhoto? Photo);
public sealed record HotelCatalogRoom(Guid Id, string Name, string RoomType, int GuestCapacity, decimal NightlyRate,
  string? Description, IReadOnlyList<HotelCatalogPhoto> Photos);
public sealed record HotelCatalogDetails(HotelCatalogEntry Hotel, IReadOnlyList<HotelCatalogRoom> Rooms);
