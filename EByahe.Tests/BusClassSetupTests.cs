using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task BusClassMigrationPreservesExistingClassesAndCanBeReversed()
  {
    await using var db = CreateDatabase();
    var migrator = db.GetService<IMigrator>();
    const string previous = "20261117104328_NormalizeCodeDecodeAndAuditUsers";
    await migrator.MigrateAsync(previous);
    var partner = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Partners] ([Id], [Code], [DisplayName], [CodeDecodePartnerStatus], [IsDemo]) VALUES ({partner}, N'MIGRATION-BUS', N'Migration operator', 1, 0)");
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [Buses] ([Id], [PartnerId], [FleetNumber], [BusClass], [SeatCapacity], [IsActive], [IsDemo]) VALUES ({first}, {partner}, N'BUS-001', N'Deluxe', 40, 1, 0), ({second}, {partner}, N'BUS-002', N'Legacy Sleeper', 30, 1, 0)");
    await migrator.MigrateAsync();
    Assert.False(db.Database.HasPendingModelChanges());
    var buses = await db.Buses.AsNoTracking().Include(bus => bus.BusClass).OrderBy(bus => bus.FleetNumber).ToArrayAsync();
    Assert.Equal(2, buses.Length); Assert.Equal(first, buses[0].Id); Assert.Equal(second, buses[1].Id);
    Assert.Equal(2, buses[0].CodeDecodeBusClass); Assert.Equal("Deluxe", buses[0].BusClass.Decode);
    Assert.Equal("Legacy Sleeper", buses[1].BusClass.Decode); Assert.True(buses[1].CodeDecodeBusClass > 10);
    Assert.Equal("System", buses[1].BusClass.CreatedBy);
    await migrator.MigrateAsync(previous);
    var restored = await db.Database.SqlQueryRaw<string>("SELECT [BusClass] AS [Value] FROM [Buses] ORDER BY [FleetNumber]").ToArrayAsync();
    Assert.Equal(new[] { "Deluxe", "Legacy Sleeper" }, restored);
    await migrator.MigrateAsync();
    Assert.Equal(2, await db.Buses.CountAsync());
  }

  [SqlServerFact]
  public async Task BusClassesComeFromSetupAndBothCreationMethodsRejectUnknownClasses()
  {
    var partner = await SeedMemberPartner();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var seeded = (await client.GetFromJsonAsync<BusClassOption[]>("/api/partner/buses/classes"))!;
    Assert.Equal(10, seeded.Length); Assert.Equal(new BusClassOption(2, "Deluxe"), seeded[1]);
    await using var db = CreateDatabase(); var custom = new CodeDecodeBusClass { Decode = "Sleeper" };
    db.CodeDecodeBusClasses.Add(custom); await db.SaveChangesAsync();
    Assert.Equal(11, custom.Code);
    Assert.Contains((await client.GetFromJsonAsync<BusClassOption[]>("/api/partner/buses/classes"))!, row => row.Code == custom.Code && row.Decode == "Sleeper");
    var command = new { partnerId = partner.Id, fleetNumber = "BUS-001", plateNumber = "AAA-1001", busClass = "Sleeper", seatCapacity = 30 };
    using var added = await client.PostAsJsonAsync("/api/partner/buses", command);
    Assert.Equal(HttpStatusCode.Created, added.StatusCode);
    Assert.Equal("Sleeper", (await added.Content.ReadFromJsonAsync<PartnerPortalBus>())!.BusClass);
    Assert.Equal(custom.Code, (await db.Buses.AsNoTracking().SingleAsync()).CodeDecodeBusClass);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/buses", command with { fleetNumber = "BUS-002", plateNumber = "AAA-1002", busClass = "Unknown" })).StatusCode);
    using var invalid = BusUpload(partner.Id, ["BUS-002", "AAA-1002", "Deluxe", "40"], ["BUS-003", "AAA-1003", "Unknown", "40"]);
    using var rejected = await client.PostAsync("/api/partner/buses/import", invalid);
    Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    var errors = (await rejected.Content.ReadFromJsonAsync<PartnerBusImportResult>())!;
    Assert.Equal(0, errors.ImportedCount); Assert.Contains(errors.Errors, row => row.Row == 3 && row.Field == "Bus class");
    Assert.Single(await db.Buses.ToArrayAsync());
    using var valid = BusUpload(partner.Id, ["BUS-002", "AAA-1002", "Sleeper", "40"]);
    Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/partner/buses/import", valid)).StatusCode);
    Assert.All(await db.Buses.AsNoTracking().ToArrayAsync(), bus => Assert.Equal(custom.Code, bus.CodeDecodeBusClass));
    var unknownCode = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE [Buses] SET [CodeDecodeBusClass] = -1"));
    Assert.Equal(547, unknownCode.Number);
  }
}
