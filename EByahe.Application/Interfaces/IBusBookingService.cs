using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;

namespace EByahe.Application.Interfaces;

public interface IBusBookingService
{
  Task<int?> AvailableSeatsAsync(Guid id, string userId, CancellationToken cancellationToken);
  Task<IReadOnlyList<EByahe.Domain.Models.BusBookingStatusLog>> HistoryAsync(Guid id, string userId, CancellationToken cancellationToken);
  Task<(BusBookingResponse Booking, bool Created)> SubmitAsync(CreateBusBookingRequest request, string userId, CancellationToken cancellationToken);
  Task<BusBookingDetailsResponse?> GetAsync(Guid id, string userId, CancellationToken cancellationToken);
  Task<BusBookingDetailsResponse?> GetByReferenceAsync(string reference, string userId, CancellationToken cancellationToken);
  Task<BusBookingPage> ListAsync(string userId, int page, int pageSize, CancellationToken cancellationToken);
}
