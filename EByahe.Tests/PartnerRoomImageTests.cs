using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static readonly byte[] RoomPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jXioAAAAASUVORK5CYII=");
  private static MultipartFormDataContent RoomForm(SavePartnerRoom command, (string Name, byte[] Bytes)[] photos,
    IEnumerable<Guid>? retained = null, bool? active = null)
  {
    var form = new MultipartFormDataContent();
    foreach (var field in new Dictionary<string, string> { ["hotelId"] = command.HotelId.ToString(), ["name"] = command.Name,
      ["roomType"] = command.RoomType, ["guestCapacity"] = command.GuestCapacity.ToString(CultureInfo.InvariantCulture),
      ["nightlyRate"] = command.NightlyRate.ToString(CultureInfo.InvariantCulture), ["description"] = command.Description ?? "" })
      form.Add(new StringContent(field.Value), field.Key);
    if (active.HasValue) form.Add(new StringContent(active.Value ? "true" : "false"), "isActive");
    foreach (var id in retained ?? []) form.Add(new StringContent(id.ToString()), "retainedImageIds");
    foreach (var (name, bytes) in photos) form.Add(new ByteArrayContent(bytes), "images", name);
    return form;
  }

  [SqlServerFact]
  public async Task RoomImagesPersistInPrivateStorageWithMetadataHistoryAndSoftRemoval()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id); var command = RoomCommand(hotel.Id);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    using var form = RoomForm(command, [("bed.png", RoomPng), ("bathroom.png", RoomPng)]);
    using var created = await client.PostAsync("/api/partner/rooms", form); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var room = (await created.Content.ReadFromJsonAsync<PartnerRoom>())!; Assert.Equal(2, room.Images!.Count);
    Assert.Equal(2, factory.Storage.Objects.Count); Assert.All(factory.Storage.Objects.Keys, key => Assert.StartsWith("ebyahe/room-images/", key));
    var json = await created.Content.ReadAsStringAsync(); Assert.DoesNotContain("storageKey", json); Assert.DoesNotContain("storageBucket", json);
    var path = "/api/partner/rooms/" + room.Id; var imagePath = path + "/images/" + room.Images[0].Id;
    using var photo = await client.GetAsync(imagePath); Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
    Assert.Equal("image/png", photo.Content.Headers.ContentType!.MediaType); Assert.Equal(RoomPng, await photo.Content.ReadAsByteArrayAsync());
    Assert.Contains("no-store", photo.Headers.CacheControl!.ToString()); Assert.Equal("nosniff", Assert.Single(photo.Headers.GetValues("X-Content-Type-Options")));
    var listed = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerRoom>>("/api/partner/rooms"))!;
    Assert.Equal(room.Images.Select(image => image.Id), Assert.Single(listed.Items).Images!.Select(image => image.Id));
    using var edit = RoomForm(command, [("view.png", RoomPng)], [room.Images[1].Id], active: true);
    using var updated = await client.PutAsync(path, edit); Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    var changed = (await updated.Content.ReadFromJsonAsync<PartnerRoom>())!;
    Assert.Equal(new[] { "bathroom.png", "view.png" }, changed.Images!.Select(image => image.FileName));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(imagePath)).StatusCode);
    Assert.Equal(3, factory.Storage.Objects.Count); // Removed versions remain available to retained audit history.
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, RoomUpdate(command))).StatusCode);
    Assert.Equal(2, (await client.GetFromJsonAsync<PartnerRoom>(path))!.Images!.Count);
    await using var db = CreateDatabase(); Assert.Equal(3, await db.RoomImages.CountAsync());
    var removed = await db.RoomImages.SingleAsync(image => image.Id == room.Images[0].Id);
    Assert.True(removed.IsDeleted); Assert.Equal(PartnerApiFactory.Subject, removed.DeletedBy);
    var log = await db.RoomLogs.Where(log => log.RoomId == room.Id && log.Action == "Created").SingleAsync();
    using var audit = JsonDocument.Parse(log.CurrentValue); Assert.Equal(2, audit.RootElement.GetProperty("images").GetArrayLength());
    Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/images/" + changed.Images![0].Id)).StatusCode);
    Assert.Equal(3, await db.RoomImages.CountAsync()); Assert.False(db.Database.HasPendingModelChanges());
  }

  [SqlServerFact]
  public async Task RoomImagesValidateAllFilesAndScopeBeforeWritingOrReadingStorage()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id); var command = RoomCommand(hotel.Id);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    foreach (var files in new (string Name, byte[] Bytes)[][] {
      [("valid.png", RoomPng), ("script.svg", "<svg/>"u8.ToArray())], [("wrong.png", "plain text"u8.ToArray())],
      [("empty.png", [])], [("large.png", new byte[5 * 1024 * 1024 + 1])],
      Enumerable.Range(0, 9).Select(i => ($"{i}.png", RoomPng)).ToArray() }) {
      using var invalid = RoomForm(command, files);
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/partner/rooms", invalid)).StatusCode);
      Assert.Empty(factory.Storage.Objects); Assert.Equal(0, factory.Storage.PutCalls);
    }
    using var valid = RoomForm(command, [("photo.png", RoomPng)]);
    using var saved = await client.PostAsync("/api/partner/rooms", valid); var room = (await saved.Content.ReadFromJsonAsync<PartnerRoom>())!;
    var path = "/api/partner/rooms/" + room.Id; var imagePath = path + "/images/" + room.Images![0].Id;
    var reads = factory.Storage.ReadCalls; var puts = factory.Storage.PutCalls;
    using var foreignRetention = RoomForm(command, [("new.png", RoomPng)], [Guid.NewGuid()], true);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync(path, foreignRetention)).StatusCode); Assert.Equal(puts, factory.Storage.PutCalls);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "another-room-user", resourceAccess: BusRoles("room.read", "room.write")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(imagePath)).StatusCode);
    using var foreignAdd = RoomForm(command, [("photo.png", RoomPng)]);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/rooms", foreignAdd)).StatusCode); Assert.Equal(puts, factory.Storage.PutCalls);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read")));
    using var readOnly = RoomForm(command, [("photo.png", RoomPng)]);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/rooms", readOnly)).StatusCode);
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(imagePath)).StatusCode); Assert.Equal(reads, factory.Storage.ReadCalls);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read")));
    factory.Storage.CorruptReads = true; Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(imagePath)).StatusCode);
  }

  [SqlServerFact]
  public async Task RoomImagesCleanUpPartialUploadsAndRollBackFailedRoomSaves()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id); var command = RoomCommand(hotel.Id);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    factory.Storage.FailAtPut = 2;
    using var fail = RoomForm(command, [("one.png", RoomPng), ("two.png", RoomPng)]);
    Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/partner/rooms", fail)).StatusCode);
    Assert.Empty(factory.Storage.Objects);
    await using var db = CreateDatabase(); Assert.Equal(0, await db.Rooms.CountAsync()); Assert.Equal(0, await db.RoomLogs.CountAsync()); Assert.Equal(0, await db.RoomImages.CountAsync());
    factory.Storage.FailAtPut = 0;
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/rooms", command)).StatusCode);
    using var duplicate = RoomForm(command, [("one.png", RoomPng)]);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/partner/rooms", duplicate)).StatusCode);
    Assert.Empty(factory.Storage.Objects); Assert.Equal(1, await db.Rooms.CountAsync()); Assert.Equal(1, await db.RoomLogs.CountAsync()); Assert.Equal(0, await db.RoomImages.CountAsync());
  }
}
