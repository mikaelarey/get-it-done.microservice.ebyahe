using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerHotelRepository
{
  Task<PartnerRecordsPage<PartnerHotel>> ListAsync(string userId, int page, int pageSize, string? search, CancellationToken ct);
  Task<IReadOnlyList<PartnerHotelWorkspace>> WorkspacesAsync(string userId, CancellationToken ct);
  Task<PartnerHotel?> AddAsync(string userId, AddPartnerHotel command, CancellationToken ct);
  Task<PartnerHotel?> GetAsync(string userId, Guid id, CancellationToken ct);
  Task<PartnerHotel?> UpdateAsync(string userId, Guid id, AddPartnerHotel command, bool isActive, CancellationToken ct);
  Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct);
}
