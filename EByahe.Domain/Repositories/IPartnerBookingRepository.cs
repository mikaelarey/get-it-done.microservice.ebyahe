using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerBookingRepository
{
  Task<PartnerBookingTypes> TypesAsync(string userId, CancellationToken ct);
  Task<PartnerRecordsPage<PartnerHotelBooking>> HotelsAsync(string userId, int page, int pageSize, string? search, CancellationToken ct);
  Task<PartnerHotelBooking?> HotelDetailsAsync(string userId, Guid id, CancellationToken ct);
}
