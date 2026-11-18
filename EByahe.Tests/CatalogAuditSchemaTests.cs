using EByahe.Domain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task CatalogAuditMigrationUsesLinkRecordsPreservesCatalogAndEnforcesForeignKeysAndAppendOnlyHistory()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var trip = new ScheduledTrip { PartnerId = partner.Id, BusId = bus.Id,
      BusRoute = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 },
      DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(3), BaseFare = 500, AvailableSeats = 40 };
    db.ScheduledTrips.Add(trip); await db.SaveChangesAsync();
    var counts = (await db.Buses.CountAsync(), await db.BusTerminals.CountAsync(), await db.ScheduledTrips.CountAsync(), await db.RefUsers.CountAsync());
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261118034937_LinkLocationsToCodeDecodeGeography");
    await migrator.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    Assert.Equal(counts, (await db.Buses.CountAsync(), await db.BusTerminals.CountAsync(), await db.ScheduledTrips.CountAsync(), await db.RefUsers.CountAsync()));
    Assert.Equal(new[] { "Buses", "Terminals", "ScheduledTrips", "Users" }, await db.CodeDecodeUploadTypes.OrderBy(row => row.Code).Select(row => row.Decode).ToArrayAsync());
    Assert.All(await db.CodeDecodeUploadTypes.ToArrayAsync(), row => { Assert.Equal("System", row.CreatedBy); Assert.NotEqual(Guid.Empty, row.Id); });
    foreach (var type in new[] { typeof(Bus), typeof(BusTerminal), typeof(ScheduledTrip), typeof(RefUser) })
      Assert.Null(db.Model.FindEntityType(type)!.FindProperty("UploadId"));
    Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.columns WHERE object_id IN (OBJECT_ID('dbo.Buses'),OBJECT_ID('dbo.BusTerminals'),OBJECT_ID('dbo.ScheduledTrips'),OBJECT_ID('dbo.RefUsers')) AND name='UploadId'").SingleAsync());
    Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.object_id=OBJECT_ID('dbo.CodeDecodeUploadTypes') AND i.is_unique=1 AND ic.key_ordinal=1 AND c.name='Code'").SingleAsync());
    await db.Database.ExecuteSqlRawAsync("INSERT INTO CodeDecodeUploadTypes (Id, Decode) VALUES (NEWID(), N'Future type')");
    Assert.Equal(5, (await db.CodeDecodeUploadTypes.SingleAsync(row => row.Decode == "Future type")).Code);
    var uploads = Enumerable.Range(1, 4).Select(code => new Upload { UploadType = code, CreatedBy = PartnerApiFactory.Subject }).ToArray();
    db.Uploads.AddRange(uploads); await db.SaveChangesAsync();
    var member = await db.RefUsers.SingleAsync(row => row.ExternalUserId == PartnerApiFactory.Subject);
    var links = new[] {
      new UploadRecord { UploadId = uploads[0].Id, BusId = bus.Id }, new UploadRecord { UploadId = uploads[1].Id, TerminalId = origin.Id },
      new UploadRecord { UploadId = uploads[2].Id, ScheduledTripId = trip.Id }, new UploadRecord { UploadId = uploads[3].Id, RefUserId = member.Id } };
    db.UploadRecords.AddRange(links); await db.SaveChangesAsync(); Assert.Equal(4, await db.UploadRecords.CountAsync());
    Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Uploads (Id,CreatedBy,UploadType) VALUES ({Guid.NewGuid()},N'unknown-user',1)"))).Number);
    Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Uploads (Id,CreatedBy,UploadType) VALUES ({Guid.NewGuid()},N'System',999)"))).Number);
    Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO UploadRecords (Id,UploadId) VALUES ({Guid.NewGuid()},{uploads[0].Id})"))).Number);
    Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO UploadRecords (Id,UploadId,BusId,TerminalId) VALUES ({Guid.NewGuid()},{uploads[2].Id},{bus.Id},{origin.Id})"))).Number);
    Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO UploadRecords (Id,UploadId,BusId) VALUES ({Guid.NewGuid()},{uploads[0].Id},{Guid.NewGuid()})"))).Number);
    var duplicate = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO UploadRecords (Id,UploadId,BusId) VALUES ({Guid.NewGuid()},{uploads[0].Id},{bus.Id})"));
    Assert.Contains(duplicate.Number, new[] { 2601, 2627 });
    var busLog = new BusLog { BusId = bus.Id, CreatedBy = PartnerApiFactory.Subject, Action = "Created", CurrentValue = "{}" };
    var terminalLog = new TerminalLog { TerminalId = origin.Id, CreatedBy = PartnerApiFactory.Subject, Action = "Created", CurrentValue = "{}" };
    var tripLog = new ScheduledTripLog { ScheduledTripId = trip.Id, CreatedBy = PartnerApiFactory.Subject, Action = "Created", CurrentValue = "{}" };
    db.AddRange(busLog, terminalLog, tripLog); await db.SaveChangesAsync();
    foreach (var row in new object[] { busLog, terminalLog, tripLog, uploads[0], links[0] })
    {
      // Load only the target to test the audit guard independently of EF's tracked-dependent delete protection.
      await using var check = CreateDatabase();
      var target = await check.FindAsync(row.GetType(), db.Entry(row).Property("Id").CurrentValue); Assert.NotNull(target);
      var entry = check.Entry(target); entry.State = EntityState.Modified;
      Assert.Contains("append-only", (await Assert.ThrowsAsync<InvalidOperationException>(() => check.SaveChangesAsync())).Message);
      entry.State = EntityState.Unchanged; entry.State = EntityState.Deleted;
      Assert.Contains("append-only", (await Assert.ThrowsAsync<InvalidOperationException>(() => check.SaveChangesAsync())).Message);
    }
  }
}
