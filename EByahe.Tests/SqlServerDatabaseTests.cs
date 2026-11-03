using EByahe.Domain.Models;
using EByahe.Infrastructure;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EByahe.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
  public SqlServerFactAttribute()
  {
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EBYAHE_TEST_SQL_SERVER")))
      Skip = "Set EBYAHE_TEST_SQL_SERVER to run against an isolated SQL Server test database.";
  }
}

public sealed partial class SqlServerDatabaseTests : IAsyncLifetime
{
  private readonly string databaseName = $"Getitdone.Ebyahe.Tests.{Guid.NewGuid():N}";
  private ServiceProvider? services;
  private string ConnectionString => new SqlConnectionStringBuilder
  {
    DataSource = Environment.GetEnvironmentVariable("EBYAHE_TEST_SQL_SERVER"),
    InitialCatalog = databaseName,
    IntegratedSecurity = true,
    Encrypt = true,
    TrustServerCertificate = true
  }.ConnectionString;

  public async Task InitializeAsync()
  {
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EBYAHE_TEST_SQL_SERVER"))) return;
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:EByaheDatabase"] = ConnectionString
    }).Build();
    services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
    await services.InitializeInfrastructureAsync();
  }

  public async Task DisposeAsync()
  {
    if (services is null) return;
    await services.DisposeAsync();
    // Only this test instance's generated database is eligible for removal.
    if (!databaseName.StartsWith("Getitdone.Ebyahe.Tests.", StringComparison.Ordinal)
      || !Guid.TryParseExact(databaseName["Getitdone.Ebyahe.Tests.".Length..], "N", out _))
      throw new InvalidOperationException("Unexpected test database name.");
    await using var database = CreateDatabase();
    await database.Database.EnsureDeletedAsync();
  }

  private EByaheDbContext CreateDatabase() => new(new DbContextOptionsBuilder<EByaheDbContext>()
    .UseSqlServer(ConnectionString).Options);

  [SqlServerFact]
  public async Task StartupCreatesMigratedDatabaseAndCanRunAgain()
  {
    await using var database = CreateDatabase();
    var applied = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
    Assert.NotEmpty(applied);
    Assert.False(database.Database.HasPendingModelChanges());
    await services!.InitializeInfrastructureAsync();
    Assert.Equal(applied, (await database.Database.GetAppliedMigrationsAsync()).ToArray());
    Assert.Empty(await database.Database.GetPendingMigrationsAsync());
    Assert.Empty(await database.RefUsers.ToListAsync());
    Assert.Empty(await database.Incoming.ToListAsync());
    Assert.Empty(await database.IncomingLogs.ToListAsync());
    Assert.Empty(await database.Outgoing.ToListAsync());
    Assert.Empty(await database.OutboxMessages.ToListAsync());
  }

  [SqlServerFact]
  public async Task RefUsersHaveAccountDefaultsAndCaseInsensitiveUniqueEmails()
  {
    await using var database = CreateDatabase();
    await database.Database.ExecuteSqlRawAsync("""
      INSERT INTO [RefUsers] ([Id], [FirstName], [LastName], [Email], [CreatedAtUtc])
      VALUES (N'user-1', N'Maria', N'Santos', N'Maria@example.com', SYSDATETIMEOFFSET());
      """);
    var user = await database.RefUsers.SingleAsync();
    Assert.Equal("Get It Done", user.Provider);
    Assert.Equal("Normal", user.Role);
    Assert.Null(await database.RefUsers.SingleOrDefaultAsync(row => row.Id == "USER-1"));

    database.RefUsers.Add(new RefUser
    {
      Id = "user-2", FirstName = "Juan", LastName = "Cruz",
      Email = "MARIA@EXAMPLE.COM", CreatedAtUtc = DateTimeOffset.UtcNow
    });
    var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    Assert.Contains(((SqlException)duplicate.InnerException!).Number, new[] { 2601, 2627 });
    database.ChangeTracker.Clear();

    var invalidRole = await Assert.ThrowsAsync<SqlException>(() => database.Database.ExecuteSqlRawAsync(
      "UPDATE [RefUsers] SET [Role] = N'normal' WHERE [Id] = N'user-1'"));
    Assert.Equal(547, invalidRole.Number);
  }

  [SqlServerFact]
  public async Task KafkaLogsRejectMutationsAndOutboxRemainsMutable()
  {
    await using var database = CreateDatabase();
    var now = DateTime.UtcNow;
    var message = new OutboxMessage
    {
      Id = Guid.NewGuid(), UserId = "user-1", EventType = "TestEvent",
      Topic = "test.ebyahe", MessageKey = "user-1", Payload = "{}",
      CreatedDateTime = now, NextAttemptDateTime = now
    };
    var incoming = new Incoming
    {
      Id = Guid.NewGuid(), DeliveryId = Guid.NewGuid(), ConsumerGroup = "ebyahe-tests",
      Topic = message.Topic, Status = "Received", ReceivedDateTime = now, RecordedDateTime = now
    };
    var incomingLog = new IncomingLog
    {
      Id = Guid.NewGuid(), IncomingId = incoming.Id, DeliveryId = incoming.DeliveryId,
      Status = "Received", RecordedDateTime = now
    };
    database.AddRange(message, incoming, incomingLog, Outgoing.Record(message, "Pending", now));
    await database.SaveChangesAsync();
    message.Status = "Publishing";
    message.AttemptCount++;
    await database.SaveChangesAsync();

    var outgoing = await database.Outgoing.SingleAsync();
    foreach (var log in new object[] { incomingLog, outgoing })
    {
      foreach (var state in new[] { EntityState.Modified, EntityState.Deleted })
      {
        database.Entry(log).State = state;
        Assert.Throws<InvalidOperationException>(() => database.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(() => database.SaveChangesAsync());
        database.Entry(log).State = EntityState.Unchanged;
      }
    }
    await database.SaveChangesAsync();
    Assert.Equal(1, await database.Incoming.CountAsync());
    Assert.Equal(1, await database.Outgoing.CountAsync());

    var duplicate = new Incoming
    {
      Id = Guid.NewGuid(), DeliveryId = incoming.DeliveryId, ConsumerGroup = incoming.ConsumerGroup,
      Topic = incoming.Topic, Status = incoming.Status, ReceivedDateTime = now, RecordedDateTime = now
    };
    database.Incoming.Add(duplicate);
    var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    Assert.Contains(((SqlException)exception.InnerException!).Number, new[] { 2601, 2627 });
    database.ChangeTracker.Clear();

    var invalidStatus = await Assert.ThrowsAsync<SqlException>(() => database.Database.ExecuteSqlRawAsync(
      "UPDATE [OutboxMessages] SET [Status] = 'Invalid'"));
    Assert.Equal(547, invalidStatus.Number);
    var restrictedDelete = await Assert.ThrowsAsync<SqlException>(() => database.Database.ExecuteSqlRawAsync(
      "DELETE FROM [OutboxMessages]"));
    Assert.Equal(547, restrictedDelete.Number);
  }
}
