namespace EByahe.Domain.Repositories;

public interface IRecordChangePublisher
{
  Task PublishAsync(string[] resources, string? userId = null, Guid? partnerId = null,
    CancellationToken ct = default);
}
