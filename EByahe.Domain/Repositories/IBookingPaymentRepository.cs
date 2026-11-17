using EByahe.Domain.DataModels;

namespace EByahe.Domain.Repositories;

public interface IBookingPaymentRepository
{
  Task CompleteAsync(Guid incomingId, PaymentCompletedEvent payment, DateTime now, CancellationToken cancellationToken);
}
