using EByahe.Infrastructure.Initialization;
using Microsoft.Extensions.DependencyInjection;

namespace EByahe.Infrastructure;

public static class InfrastructureInitializationExtensions
{
  public static async Task InitializeInfrastructureAsync(this IServiceProvider services,
    CancellationToken cancellationToken = default)
  {
    await using var scope = services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<EByaheInitializer>().InitializeAsync(cancellationToken);
  }
}
