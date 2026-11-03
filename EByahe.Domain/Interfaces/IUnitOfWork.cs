namespace EByahe.Domain.Interfaces;

public interface IUnitOfWork
{
  Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
