using EByahe.Infrastructure.Initialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EByahe.Tests;

public sealed class DatabaseInitializationTests
{
  [Theory]
  [InlineData(true, 1)]
  [InlineData(false, 0)]
  public async Task StartupMigrationFollowsConfiguration(bool enabled, int expectedCalls)
  {
    var migrations = new TestMigrator();
    using var cancellation = new CancellationTokenSource();
    var initializer = new EByaheInitializer(migrations,
      Options.Create(new DatabaseInitializationOptions { MigrateOnStartup = enabled }),
      NullLogger<EByaheInitializer>.Instance);

    await initializer.InitializeAsync(cancellation.Token);

    Assert.Equal(expectedCalls, migrations.Calls);
    if (enabled) Assert.Equal(cancellation.Token, migrations.Token);
  }

  [Fact]
  public async Task MigrationFailureStopsStartup()
  {
    var initializer = new EByaheInitializer(new TestMigrator { Fail = true },
      Options.Create(new DatabaseInitializationOptions()), NullLogger<EByaheInitializer>.Instance);
    await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.InitializeAsync());
  }

  private sealed class TestMigrator : IDatabaseMigrator
  {
    public int Calls { get; private set; }
    public CancellationToken Token { get; private set; }
    public bool Fail { get; init; }

    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
      Calls++;
      Token = cancellationToken;
      if (Fail) throw new InvalidOperationException("Test migration failure");
      return Task.CompletedTask;
    }
  }
}
