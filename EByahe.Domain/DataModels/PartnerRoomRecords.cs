using EByahe.Domain.Models;
namespace EByahe.Domain.DataModels;

public sealed record PartnerRoom(Guid Id, Guid HotelId, string Hotel, Guid PartnerId, string Partner,
  string Name, string RoomType, int GuestCapacity, decimal NightlyRate, string? Description, bool IsActive, bool HotelIsActive,
  IReadOnlyList<PartnerRoomImage>? Images = null);
public sealed record PartnerRoomImage(Guid Id, string FileName, string ContentType, long SizeBytes);
public sealed record RoomTypeOption(int Code, string Decode);
public sealed record PartnerRoomHotel(Guid Id, string Name, string Partner, bool IsActive);
public sealed record SavePartnerRoom(Guid HotelId, string Name, string RoomType, int GuestCapacity, decimal NightlyRate, string? Description,
  IReadOnlyList<RoomImage>? NewImages = null, IReadOnlyList<Guid>? RetainedImageIds = null);
