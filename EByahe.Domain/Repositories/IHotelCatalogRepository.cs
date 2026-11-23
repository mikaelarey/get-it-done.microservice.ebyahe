using EByahe.Domain.DataModels;
using EByahe.Domain.Models;

namespace EByahe.Domain.Repositories;

public interface IHotelCatalogRepository
{
  Task<PartnerRecordsPage<HotelCatalogEntry>> SearchAsync(string? search, int guests, int? roomType, string sort, int page, CancellationToken ct);
  Task<IReadOnlyList<RoomTypeOption>> TypesAsync(CancellationToken ct);
  Task<HotelCatalogDetails?> GetAsync(Guid id, int guests, int? roomType, CancellationToken ct);
  Task<RoomImage?> GetImageAsync(Guid roomId, Guid imageId, CancellationToken ct);
}
