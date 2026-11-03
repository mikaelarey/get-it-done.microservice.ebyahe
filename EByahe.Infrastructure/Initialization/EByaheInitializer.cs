using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EByahe.Infrastructure.Initialization;

public sealed class EByaheInitializer(IDatabaseMigrator migrations,
  IOptions<DatabaseInitializationOptions> options, ILogger<EByaheInitializer> logger)
{
  public async Task InitializeAsync(CancellationToken cancellationToken = default)
  {
    if (options.Value.MigrateOnStartup)
    {
      logger.LogInformation("Applying pending EByahe database migrations.");
      await migrations.MigrateAsync(cancellationToken);
    }

    logger.LogInformation("EByahe infrastructure initialization completed.");
  }
}
