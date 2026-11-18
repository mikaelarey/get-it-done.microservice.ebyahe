using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task BusUniqueIndexesRejectDuplicatePlatesAndMigrationPreservesBuses()
  {
    var partner = await SeedMemberPartner();
    await using var db = CreateDatabase();
    var foreign = new Partner { Code = "UNIQUE-OTHER", DisplayName = "Other bus operator" };
    var bus = new Bus { PartnerId = partner.Id, FleetNumber = "BUS-001", PlateNumber = "ABC-1234", CodeDecodeBusClass = 2 };
    db.Partners.Add(foreign); db.Buses.Add(bus); await db.SaveChangesAsync();
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261118023026_AddCodeDecodeBusClasses");
    await migrator.MigrateAsync();
    Assert.False(db.Database.HasPendingModelChanges());
    Assert.Equal(bus.PlateNumber, (await db.Buses.AsNoTracking().SingleAsync()).PlateNumber);
    var columns = await db.Database.SqlQueryRaw<string>("""
      SELECT c.[name] AS [Value]
      FROM sys.indexes i
      JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
      JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
      WHERE i.object_id = OBJECT_ID('dbo.Buses')
        AND i.[name] = 'IX_Buses_PartnerId_FleetNumber_PlateNumber' AND i.is_unique = 1
      ORDER BY ic.key_ordinal
      """).ToArrayAsync();
    Assert.Equal(new[] { "PartnerId", "FleetNumber", "PlateNumber" }, columns);
    foreach (var duplicate in new[] {
      (Partner: partner.Id, Fleet: "BUS-001", Plate: "ABC-1234"),
      (Partner: partner.Id, Fleet: "BUS-002", Plate: "abc-1234"),
      (Partner: foreign.Id, Fleet: "OTHER-001", Plate: "ABC-1234") })
    {
      var id = Guid.NewGuid();
      var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO [Buses] ([Id], [PartnerId], [FleetNumber], [PlateNumber], [CodeDecodeBusClass], [SeatCapacity], [IsActive], [IsDemo])
        VALUES ({id}, {duplicate.Partner}, {duplicate.Fleet}, {duplicate.Plate}, 2, 40, 1, 0)
        """));
      Assert.Contains(error.Number, new[] { 2601, 2627 });
    }
    Assert.Single(await db.Buses.ToArrayAsync());
  }

  [SqlServerFact]
  public async Task BusCreationAndImportRejectPlatesOwnedByAnotherPartnerWithoutPartialWrites()
  {
    var partner = await SeedMemberPartner();
    await using var db = CreateDatabase();
    var foreign = new Partner { Code = "UNIQUE-OTHER", DisplayName = "Other bus operator" };
    db.Buses.Add(new Bus { Partner = foreign, FleetNumber = "BUS-001", PlateNumber = "ABC-1234", CodeDecodeBusClass = 2 });
    await db.SaveChangesAsync();
    var initialEvents = await db.OutboxMessages.CountAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var command = new { partnerId = partner.Id, fleetNumber = "BUS-001", plateNumber = "abc-1234", busClass = "Deluxe", seatCapacity = 40 };
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/buses", command)).StatusCode);
    using var file = BusUpload(partner.Id, ["BUS-001", "NEW-1001", "Deluxe", "40"], ["BUS-002", " abc-1234 ", "Deluxe", "40"]);
    using var response = await client.PostAsync("/api/partner/buses/import", file);
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    var rejected = (await response.Content.ReadFromJsonAsync<PartnerBusImportResult>())!;
    Assert.Equal(0, rejected.ImportedCount);
    Assert.Contains(rejected.Errors, error => error.Row == 3 && error.Field == "Plate number");
    Assert.Single(await db.Buses.ToArrayAsync()); Assert.Equal(initialEvents, await db.OutboxMessages.CountAsync());
    // Different partners can reuse fleet numbers when their plates are different.
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/buses", command with { plateNumber = "NEW-1001" })).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/buses", command with { plateNumber = "NEW-1001" })).StatusCode);
    Assert.Equal(2, await db.Buses.CountAsync()); Assert.Equal(initialEvents + 1, await db.OutboxMessages.CountAsync());
  }
}
