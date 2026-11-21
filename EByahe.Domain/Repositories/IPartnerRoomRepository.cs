using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerRoomRepository
{
  Task<PartnerRecordsPage<PartnerRoom>> ListAsync(string userId, int page, int pageSize, string? search, Guid? hotelId, CancellationToken ct);
  Task<IReadOnlyList<PartnerRoomHotel>> HotelsAsync(string userId, CancellationToken ct);
  Task<IReadOnlyList<RoomTypeOption>> TypesAsync(CancellationToken ct);
  Task<PartnerRoom?> GetAsync(string userId, Guid id, CancellationToken ct);
  Task<EByahe.Domain.Models.RoomImage?> GetImageAsync(string userId, Guid roomId, Guid imageId, CancellationToken ct);
  Task<bool> AnyImageExistsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
  Task<PartnerRoom?> AddAsync(string userId, SavePartnerRoom command, CancellationToken ct);
  Task<PartnerRoom?> UpdateAsync(string userId, Guid id, SavePartnerRoom command, bool isActive, CancellationToken ct);
  Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct);
}
