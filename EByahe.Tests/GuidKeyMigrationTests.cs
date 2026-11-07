using System.Text.Json;
using System.Text.Json.Nodes;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private async Task ResetToMigrationAsync(EByaheDbContext db, string migration)
  {
    // Upgrade fixtures start from a fresh, isolated test database, rather than
    // attempting a lossy rollback of already-converted application keys.
    if (!databaseName.StartsWith("Getitdone.Ebyahe.Tests.", StringComparison.Ordinal)
      || !Guid.TryParseExact(databaseName["Getitdone.Ebyahe.Tests.".Length..], "N", out _))
      throw new InvalidOperationException("Unexpected test database name.");
    await db.Database.EnsureDeletedAsync();
    await db.GetService<IMigrator>().MigrateAsync(migration);
  }

  [SqlServerFact]
  public async Task GuidMigrationPreservesRelationshipsReservationsSnapshotsAndExternalUserIds()
  {
    await using var db = CreateDatabase();
    await ResetToMigrationAsync(db, "20261106075731_ReserveBookingSeats");
    var bookingId = Guid.NewGuid(); var historicalId = Guid.NewGuid();
    var request = ReservationRequest(2);
    var node = JsonSerializer.SerializeToNode(request, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    node["trip"]!["tripId"] = 61;
    var snapshot = node.ToJsonString();
    node["trip"]!["tripId"] = 999; node["trip"]!["origin"] = "Historical origin";
    var historical = node.ToJsonString();
    var departure = DateTimeOffset.Parse($"{request.Trip.DepartureDate}T06:00:00+08:00").ToUniversalTime();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      SET IDENTITY_INSERT Partners ON;
      INSERT Partners (Id,Code,DisplayName,Status,IsDemo) VALUES(11,'MIGRATE','Victory Liner','Approved',0);
      SET IDENTITY_INSERT Partners OFF;
      SET IDENTITY_INSERT Locations ON;
      INSERT Locations (Id,Code,Name,CountryCode,IsActive,IsDemo) VALUES(21,'MNL','Manila','PH',1,0),(22,'BAG','Baguio','PH',1,0);
      SET IDENTITY_INSERT Locations OFF;
      SET IDENTITY_INSERT BusTerminals ON;
      INSERT BusTerminals (Id,PartnerId,LocationId,Name,Address,IsActive,IsDemo)
      VALUES(31,11,21,'Manila Bus Station','Manila',1,0),(32,11,22,'Baguio Bus Station','Baguio',1,0);
      SET IDENTITY_INSERT BusTerminals OFF;
      SET IDENTITY_INSERT Buses ON;
      INSERT Buses(Id,PartnerId,FleetNumber,PlateNumber,BusClass,SeatCapacity,IsActive,IsDemo)
      VALUES(41,11,'BUS-1','TST-1234','Deluxe',40,1,0);
      SET IDENTITY_INSERT Buses OFF;
      SET IDENTITY_INSERT BusRoutes ON;
      INSERT BusRoutes(Id,PartnerId,OriginTerminalId,DestinationTerminalId,DurationMinutes,IsActive,IsDemo) VALUES(51,11,31,32,360,1,0);
      SET IDENTITY_INSERT BusRoutes OFF;
      SET IDENTITY_INSERT ScheduledTrips ON;
      INSERT ScheduledTrips(Id,PartnerId,BusId,BusRouteId,DepartureAtUtc,BaseFare,Status,IsDemo,AvailableSeats)
      VALUES(61,11,41,51,{departure},750,'Scheduled',0,38);
      SET IDENTITY_INSERT ScheduledTrips OFF;
      INSERT RefUsers(Id,FirstName,LastName,Email,CreatedAtUtc)
      VALUES('opaque-account-subject','Legacy','Traveler','legacy@example.com',SYSDATETIMEOFFSET());
      INSERT BusBookings(Id,RequestId,ScheduledTripId,CreatedBy,Reference,CreatedAtUtc,Status,Total,RequestHash,DetailsJson,ReservedSeats)
      VALUES({bookingId},{request.RequestId},61,'opaque-account-subject','BK-GUID-MIGRATION',SYSDATETIMEOFFSET(),'Submitted',1545,{new string('A',64)},{snapshot},2),
      ({historicalId},{Guid.NewGuid()},NULL,'opaque-account-subject','BK-LEGACY-SNAPSHOT',SYSDATETIMEOFFSET(),'Submitted',1545,{new string('B',64)},{historical},0);
      """);
    await db.GetService<IMigrator>().MigrateAsync();
    Assert.False(db.Database.HasPendingModelChanges());
    var partner = await db.Partners.SingleAsync(); var bus = await db.Buses.SingleAsync();
    var route = await db.BusRoutes.SingleAsync(); var trip = await db.ScheduledTrips.SingleAsync();
    var terminals = await db.BusTerminals.Include(x => x.Location).ToArrayAsync();
    Assert.NotEqual(Guid.Empty, partner.Id); Assert.Equal(partner.Id, bus.PartnerId);
    Assert.Equal(partner.Id, route.PartnerId); Assert.Equal(partner.Id, trip.PartnerId);
    Assert.Equal(bus.Id, trip.BusId); Assert.Equal(route.Id, trip.BusRouteId);
    Assert.Equal("Manila", terminals.Single(x => x.Id == route.OriginTerminalId).Location.Name);
    Assert.Equal("Baguio", terminals.Single(x => x.Id == route.DestinationTerminalId).Location.Name);
    Assert.Equal(38, trip.AvailableSeats); Assert.Equal(departure, trip.DepartureAtUtc); Assert.Equal("TST-1234", bus.PlateNumber);
    var booking = await db.BusBookings.SingleAsync(x => x.Id == bookingId);
    Assert.Equal(trip.Id, booking.ScheduledTripId); Assert.Equal(2, booking.ReservedSeats);
    Assert.Equal("BK-GUID-MIGRATION", booking.Reference); Assert.Equal(new string('A',64), booking.RequestHash);
    using var patched = JsonDocument.Parse(booking.DetailsJson);
    Assert.Equal(trip.Id, patched.RootElement.GetProperty("trip").GetProperty("tripId").GetGuid());
    Assert.Equal("Manila", patched.RootElement.GetProperty("trip").GetProperty("origin").GetString());
    var old = await db.BusBookings.SingleAsync(x => x.Id == historicalId);
    Assert.Null(old.ScheduledTripId); Assert.Equal(0, old.ReservedSeats);
    Assert.NotEqual(Guid.Empty, JsonNode.Parse(old.DetailsJson)!["trip"]!["tripId"]!.GetValue<Guid>());
    var user = await db.RefUsers.SingleAsync(); Assert.NotEqual(Guid.Empty,user.Id);
    Assert.Equal("opaque-account-subject",user.ExternalUserId); Assert.Equal("opaque-account-subject",booking.CreatedBy);
    Assert.Equal(2, await db.Locations.CountAsync()); Assert.Equal(2, await db.BusTerminals.CountAsync());
    Assert.Equal(0, await db.Database.SqlQueryRaw<int>("""
      SELECT COUNT(*) AS [Value] FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id AND i.is_primary_key=1
      JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=i.index_id
      JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
      JOIN sys.types ty ON ty.user_type_id=c.user_type_id
      WHERE t.name<>'__EFMigrationsHistory' AND ty.name<>'uniqueidentifier'
      """).SingleAsync());
    Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1").SingleAsync());
  }
}
