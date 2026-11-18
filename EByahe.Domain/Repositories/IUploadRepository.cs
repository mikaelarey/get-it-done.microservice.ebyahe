using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IUploadRepository
{
  Task<Guid> AddAsync(string userId, int uploadType, CancellationToken cancellationToken, Guid? requestedId = null);
  Task<UploadDetails?> DetailsAsync(string userId, Guid id, IReadOnlyCollection<int> allowedTypes, int page, int pageSize, CancellationToken cancellationToken);
}
