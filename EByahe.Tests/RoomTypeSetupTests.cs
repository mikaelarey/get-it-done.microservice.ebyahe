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
  public async Task RoomTypeMigrationPreservesExistingCustomAndDeletedRoomTypesAndCanBeReversed()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id);
    await using var db = CreateDatabase(); var migrator = db.GetService<IMigrator>();
    const string previous = "20261121193450_AddRoomImages";
    await migrator.MigrateAsync(previous);
    var first = Guid.NewGuid(); var second = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO [Rooms] ([Id],[HotelId],[Name],[RoomType],[GuestCapacity],[NightlyRate],[IsActive],[IsDeleted],[CreatedBy],[CreatedAtUtc],[DeletedBy],[DeletedAtUtc])
      VALUES ({first},{hotel.Id},N'Room 101',N'Deluxe',2,2500,1,0,N'System',SYSUTCDATETIME(),NULL,NULL),
        ({second},{hotel.Id},N'Room 102',N'Legacy Garden Villa',4,5000,0,1,N'System',SYSUTCDATETIME(),N'System',SYSUTCDATETIME());
      """);
    await migrator.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
    var rooms = await db.Rooms.AsNoTracking().Include(room => room.RoomType).OrderBy(room => room.Name).ToArrayAsync();
    Assert.Equal(2, rooms.Length); Assert.Equal(3, rooms[0].CodeDecodeRoomType); Assert.Equal("Deluxe", rooms[0].RoomType.Decode);
    Assert.Equal("Legacy Garden Villa", rooms[1].RoomType.Decode); Assert.True(rooms[1].CodeDecodeRoomType > 10); Assert.True(rooms[1].IsDeleted);
    Assert.Equal("System", rooms[1].RoomType.CreatedBy);
    await migrator.MigrateAsync(previous);
    var restored = await db.Database.SqlQueryRaw<string>("SELECT [RoomType] AS [Value] FROM [Rooms] ORDER BY [Name]").ToArrayAsync();
    Assert.Equal(new[] { "Deluxe", "Legacy Garden Villa" }, restored);
    await migrator.MigrateAsync(); Assert.Equal(2, await db.Rooms.CountAsync());
  }

  [SqlServerFact]
  public async Task RoomTypesComeFromSetupAndRejectUnknownTypesBeforeUploadingPhotos()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    var seeded = (await client.GetFromJsonAsync<RoomTypeOption[]>("/api/partner/rooms/types"))!;
    Assert.Equal(10, seeded.Length); Assert.Equal(new RoomTypeOption(3, "Deluxe"), seeded[2]);
    await using var db = CreateDatabase(); var custom = new CodeDecodeRoomType { Decode = "Garden Villa" };
    db.CodeDecodeRoomTypes.Add(custom); await db.SaveChangesAsync(); Assert.Equal(11, custom.Code);
    Assert.Contains((await client.GetFromJsonAsync<RoomTypeOption[]>("/api/partner/rooms/types"))!, option => option.Code == custom.Code && option.Decode == custom.Decode);
    var command = RoomCommand(hotel.Id) with { RoomType = "Garden Villa" };
    using var created = await client.PostAsJsonAsync("/api/partner/rooms", command); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var room = (await created.Content.ReadFromJsonAsync<PartnerRoom>())!; Assert.Equal("Garden Villa", room.RoomType);
    Assert.Equal(custom.Code, (await db.Rooms.AsNoTracking().SingleAsync()).CodeDecodeRoomType);
    var path = "/api/partner/rooms/" + room.Id;
    var invalid = command with { RoomType = "Unconfigured" };
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/rooms", invalid with { Name = "Room 102" })).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, RoomUpdate(invalid))).StatusCode);
    using var photos = RoomForm(invalid with { Name = "Room 102" }, [("bed.png", RoomPng)]);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/partner/rooms", photos)).StatusCode); Assert.Equal(0, factory.Storage.PutCalls);
    using var editPhotos = RoomForm(invalid, [("bed.png", RoomPng)], active: true);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync(path, editPhotos)).StatusCode); Assert.Equal(0, factory.Storage.PutCalls);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, RoomUpdate(command with { RoomType = "Suite" }))).StatusCode);
    Assert.Equal("Suite", (await client.GetFromJsonAsync<PartnerRoom>(path))!.RoomType);
    var search = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms?search=Suite"))!;
    Assert.Equal(room.Id, Assert.Single(search.Items).Id);
    var unknownCode = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("UPDATE [Rooms] SET [CodeDecodeRoomType] = -1"));
    Assert.Equal(547, unknownCode.Number);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read")));
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/partner/rooms/types")).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/rooms/types")).StatusCode);
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/partner/rooms/types")).StatusCode);
  }
}
