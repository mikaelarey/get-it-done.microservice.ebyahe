using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Application.Dtos.Requests;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private async Task<PartnerHotel> CreateRoomHotel(HttpClient client, PartnerApiFactory factory, Guid partnerId, string name = "Room Test Hotel")
  {
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    using var response = await client.PostAsJsonAsync("/api/partner/hotels", await HotelCommand(partnerId, name));
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return (await response.Content.ReadFromJsonAsync<PartnerHotel>())!;
  }
  private static SavePartnerRoom RoomCommand(Guid hotelId, string name = "Room 101") => new(hotelId, name, "Deluxe", 2, 2500.50m, "Queen bed and balcony");
  private static UpdatePartnerRoomRequest RoomUpdate(SavePartnerRoom command) => new() { HotelId = command.HotelId, Name = command.Name,
    RoomType = command.RoomType, GuestCapacity = command.GuestCapacity, NightlyRate = command.NightlyRate, Description = command.Description, IsActive = false };

  [SqlServerFact]
  public async Task PartnerRoomsCrudPersistsDataAndImmutableHistoryWithSoftDeletion()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    var command = RoomCommand(hotel.Id);
    using var created = await client.PostAsJsonAsync("/api/partner/rooms", command with { Name = " Room 101 ", RoomType = " Deluxe " });
    Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.True(created.Headers.CacheControl!.NoStore);
    var room = (await created.Content.ReadFromJsonAsync<PartnerRoom>())!; var path = "/api/partner/rooms/" + room.Id;
    Assert.Equal("Room 101", room.Name); Assert.Equal("Deluxe", room.RoomType); Assert.Equal(2500.50m, room.NightlyRate);
    Assert.Equal(hotel.Id, room.HotelId); Assert.True(room.IsActive); Assert.True(room.HotelIsActive);
    var details = (await client.GetFromJsonAsync<PartnerRoom>(path))!;
    Assert.Equal(room with { Images = null }, details with { Images = null }); Assert.Equal(room.Images, details.Images);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/rooms", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/rooms", command with { Name = "Room 102" })).StatusCode);
    var page = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms?pageSize=1"))!;
    Assert.Equal(2, page.TotalCount); Assert.Equal(room.Id, Assert.Single(page.Items).Id);
    var second = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms?pageSize=1&page=2"))!;
    Assert.NotEqual(room.Id, Assert.Single(second.Items).Id);
    var search = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms?search=queen%20101&hotelId=" + hotel.Id))!;
    Assert.Equal(room.Id, Assert.Single(search.Items).Id);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms?hotelId=" + Guid.NewGuid()))!.Items);
    var update = RoomUpdate(command); update.NightlyRate = 3200.75m; update.GuestCapacity = 3;
    using var updated = await client.PutAsJsonAsync(path, update); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    var changed = (await updated.Content.ReadFromJsonAsync<PartnerRoom>())!;
    Assert.Equal(3200.75m, changed.NightlyRate); Assert.Equal(3, changed.GuestCapacity); Assert.False(changed.IsActive);
    var deletions = await Task.WhenAll(client.DeleteAsync(path), client.DeleteAsync(path));
    Assert.All(deletions, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, update)).StatusCode);
    var remaining = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms"))!;
    Assert.Equal(1, remaining.TotalCount); Assert.DoesNotContain(remaining.Items, row => row.Id == room.Id);
    await using var db = CreateDatabase(); var saved = await db.Rooms.SingleAsync(row => row.Id == room.Id);
    Assert.True(saved.IsDeleted); Assert.Equal(PartnerApiFactory.Subject, saved.DeletedBy); Assert.NotNull(saved.DeletedAtUtc);
    Assert.Equal(PartnerApiFactory.Subject, saved.CreatedBy); Assert.Equal(hotel.Id, saved.HotelId);
    var logs = await db.RoomLogs.Where(row => row.RoomId == room.Id).OrderBy(row => row.DateTimeCreated).ToArrayAsync();
    Assert.Equal(new[] { "Created", "Updated", "Deleted" }, logs.Select(row => row.Action));
    Assert.All(logs, row => Assert.Equal(PartnerApiFactory.Subject, row.CreatedBy));
    using var old = JsonDocument.Parse(logs[1].PreviousValue!); Assert.Equal(2500.50m, old.RootElement.GetProperty("nightlyRate").GetDecimal());
    logs[0].CurrentValue = "{}"; await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
    db.Rooms.Remove(await db.Rooms.SingleAsync(row => row.Id == room.Id)); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/rooms", command)).StatusCode);
    Assert.False(db.Database.HasPendingModelChanges());
  }

  [SqlServerFact]
  public async Task PartnerRoomsEnforceRoomRolesAndValidateRatesCountsAndHotels()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id); var command = RoomCommand(hotel.Id); var path = "/api/partner/rooms/" + Guid.NewGuid();
    client.DefaultRequestHeaders.Authorization = null;
    foreach (var endpoint in new[] { "/api/partner/rooms", "/api/partner/rooms/hotels", path })
      Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(endpoint)).StatusCode);
    foreach (var roles in new[] { BusRoles("room.write"), BusRoles("hotel.read", "hotel.write"),
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin" } },
        ["get-it-done-ebyahe-api"] = new { roles = new[] { "room.read", "room.write" } } },
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "partner", "room.read", "room.write" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/rooms")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/rooms", command)).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, RoomUpdate(command))).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(path)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read")));
    Assert.Equal(hotel.Id, Assert.Single((await client.GetFromJsonAsync<PartnerRoomHotel[]>("/api/partner/rooms/hotels"))!).Id);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/rooms", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(path)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    foreach (var invalid in new[] { command with { Name = "  " }, command with { RoomType = "" }, command with { Name = "Room\n101" },
      command with { GuestCapacity = 0 }, command with { GuestCapacity = 101 }, command with { NightlyRate = -1 }, command with { NightlyRate = 1.234m }, command with { HotelId = Guid.Empty } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/rooms", invalid)).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/rooms", new { hotelId = hotel.Id, name = "Room", roomType = "Suite", guestCapacity = 2 })).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/partner/rooms?page=0")).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/partner/rooms?hotelId=" + Guid.Empty)).StatusCode);
    using var created = await client.PostAsJsonAsync("/api/partner/rooms", command with { NightlyRate = 0 });
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var room = (await created.Content.ReadFromJsonAsync<PartnerRoom>())!;
    var missingStatus = RoomUpdate(command); missingStatus.IsActive = null;
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/partner/rooms/" + room.Id, missingStatus)).StatusCode);
    await using var db = CreateDatabase(); var savedHotel = await db.Hotels.SingleAsync(); savedHotel.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/rooms", command with { Name = "Room 103" })).StatusCode);
    Assert.False((await client.GetFromJsonAsync<PartnerRoom>("/api/partner/rooms/" + room.Id))!.HotelIsActive);
  }

  [SqlServerFact]
  public async Task PartnerRoomsStayWithinActiveHotelMembershipsAndCannotMoveHotels()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id);
    var secondHotel = await CreateRoomHotel(client, factory, partner.Id, "Second Hotel");
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    var command = RoomCommand(hotel.Id);
    using var created = await client.PostAsJsonAsync("/api/partner/rooms", command); var room = (await created.Content.ReadFromJsonAsync<PartnerRoom>())!;
    var path = "/api/partner/rooms/" + room.Id; var update = RoomUpdate(command); update.HotelId = secondHotel.Id;
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, update)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "foreign-room-user", resourceAccess: BusRoles("room.read", "room.write")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms"))!.Items);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRoomHotel[]>("/api/partner/rooms/hotels"))!);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, RoomUpdate(command))).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/rooms", command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    await using var db = CreateDatabase(); var membership = await db.PartnerUsers.SingleAsync(); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    membership.IsActive = true; var application = await db.PartnerApplications.SingleAsync(); application.BusinessCategory = "bus-operator"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms"))!.Items);
    application.BusinessCategory = "hotel"; await db.SaveChangesAsync();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/partner/hotels/" + hotel.Id)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms"))!.Items);
    Assert.Equal(1, await db.Rooms.CountAsync()); Assert.Equal(1, await db.RoomLogs.CountAsync());
  }
}
