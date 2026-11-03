namespace EByahe.Infrastructure.Initialization;

public sealed class DatabaseInitializationOptions
{
  public bool MigrateOnStartup { get; set; } = true;
}
