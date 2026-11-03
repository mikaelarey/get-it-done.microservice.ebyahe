namespace EByahe.Infrastructure.Initialization;

public interface IDatabaseMigrator
{
  Task MigrateAsync(CancellationToken cancellationToken = default);
}
