using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PublicHotelCatalogFiltersPagesAndOnlyPublishesActiveApprovedHotelRoomsAndPhotos()
  {
    var partner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var hotel = await CreateRoomHotel(client, factory, partner.Id, "Seaside Retreat");
    var empty = await CreateRoomHotel(client, factory, partner.Id, "No Rooms Yet");
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("room.read", "room.write")));
    var command = RoomCommand(hotel.Id) with { NightlyRate = 2500m };
    using var form = RoomForm(command, [("bed.png", RoomPng)]);
    using var created = await client.PostAsync("/api/partner/rooms", form);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    var room = (await created.Content.ReadFromJsonAsync<PartnerRoom>())!;
    using var second = await client.PostAsJsonAsync("/api/partner/rooms", command with { Name = "Family room", RoomType = "Family", GuestCapacity = 5, NightlyRate = 4000m });
    Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    client.DefaultRequestHeaders.Authorization = null;
    var page = (await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>("/api/hotel-catalog?guests=2"))!;
    var entry = Assert.Single(page.Items); Assert.Equal(hotel.Id, entry.Id); Assert.Equal(2, entry.RoomCount); Assert.Equal(2500m, entry.FromNightlyRate);
    Assert.Equal(room.Id, entry.Photo!.RoomId);
    Assert.DoesNotContain(empty.Id, page.Items.Select(item => item.Id));
    var types = (await client.GetFromJsonAsync<RoomTypeOption[]>("/api/hotel-catalog/types"))!;
    Assert.Equal(new[] { "Deluxe", "Family" }, types.Select(type => type.Decode));
    var deluxe = types.Single(type => type.Decode == "Deluxe").Code;
    Assert.Single((await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>($"/api/hotel-catalog?search=retreat%20beach&roomType={deluxe}"))!.Items);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>($"/api/hotel-catalog?guests=3&roomType={deluxe}"))!.Items);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>("/api/hotel-catalog?search=nonexistent"))!.Items);
    var details = (await client.GetFromJsonAsync<HotelCatalogDetails>($"/api/hotel-catalog/{hotel.Id}?guests=3"))!;
    Assert.Equal("Family", Assert.Single(details.Rooms).RoomType); Assert.Equal(4000m, details.Hotel.FromNightlyRate);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/hotel-catalog/{empty.Id}")).StatusCode);
    var photoPath = $"/api/hotel-catalog/rooms/{room.Id}/images/{entry.Photo.ImageId}";
    using var photo = await client.GetAsync(photoPath); Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
    Assert.Equal(RoomPng, await photo.Content.ReadAsByteArrayAsync()); Assert.True(photo.Headers.CacheControl!.NoStore);
    var json = await client.GetStringAsync($"/api/hotel-catalog/{hotel.Id}");
    foreach (var field in new[] { "storageBucket", "storageKey", "createdBy", "sha256", "fileName" }) Assert.DoesNotContain(field, json);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/hotel-catalog/rooms/{Guid.NewGuid()}/images/{entry.Photo.ImageId}")).StatusCode);
    foreach (var query in new[] { "guests=0", "guests=101", "guests=1.5", "page=0", "roomType=0", "sort=invalid", "search=" + new string('a', 201) })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/hotel-catalog?" + query)).StatusCode);

    await using var db = CreateDatabase();
    var original = await db.Hotels.SingleAsync(item => item.Id == hotel.Id);
    for (var i = 0; i < 13; i++) {
      var extra = new Hotel { PartnerId = partner.Id, Name = $"Hotel {i:00}", Address = original.Address,
        CodeDecodeRegion = original.CodeDecodeRegion, CodeDecodeProvince = original.CodeDecodeProvince,
        CodeDecodeMunicipality = original.CodeDecodeMunicipality, CodeDecodeBarangay = original.CodeDecodeBarangay,
        ContactNumber = original.ContactNumber, Email = original.Email };
      db.Rooms.Add(new Room { Hotel = extra, Name = "Room 1", CodeDecodeRoomType = deluxe, GuestCapacity = 2, NightlyRate = 1000 + i });
    }
    await db.SaveChangesAsync();
    var low = (await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>("/api/hotel-catalog?sort=price-asc"))!;
    var more = (await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>("/api/hotel-catalog?sort=price-asc&page=2"))!;
    Assert.Equal(14, low.TotalCount); Assert.Equal(12, low.Items.Count); Assert.Equal(2, more.Items.Count);
    Assert.Empty(low.Items.Select(item => item.Id).Intersect(more.Items.Select(item => item.Id)));
    Assert.Equal(1000m, low.Items[0].FromNightlyRate); Assert.Equal(hotel.Id, more.Items.Last().Id);
    Assert.Equal(hotel.Id, (await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>("/api/hotel-catalog?sort=price-desc"))!.Items[0].Id);

    async Task AssertHidden() {
      Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>("/api/hotel-catalog?search=seaside"))!.Items);
      Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/hotel-catalog/{hotel.Id}")).StatusCode);
      var reads = factory.Storage.ReadCalls;
      Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(photoPath)).StatusCode); Assert.Equal(reads, factory.Storage.ReadCalls);
    }
    original.IsActive = false; await db.SaveChangesAsync(); await AssertHidden();
    original.IsActive = true; original.IsDeleted = true; original.DeletedBy = "System"; original.DeletedAtUtc = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(); await AssertHidden();
    original.IsDeleted = false; original.DeletedBy = null; original.DeletedAtUtc = null;
    var savedPartner = await db.Partners.SingleAsync(); savedPartner.Status = "Suspended"; await db.SaveChangesAsync(); await AssertHidden();
    savedPartner.Status = "Approved";
    var application = await db.PartnerApplications.SingleAsync(); application.BusinessCategory = "bus-operator"; await db.SaveChangesAsync(); await AssertHidden();
    application.BusinessCategory = "hotel"; application.Status = "Rejected"; await db.SaveChangesAsync(); await AssertHidden();
    application.Status = "Approved";
    foreach (var savedRoom in await db.Rooms.Where(item => item.HotelId == hotel.Id).ToArrayAsync()) savedRoom.IsActive = false;
    await db.SaveChangesAsync(); await AssertHidden();
    var saved = await db.Rooms.SingleAsync(item => item.Id == room.Id); saved.IsActive = true;
    var image = await db.RoomImages.SingleAsync(); image.IsDeleted = true; image.DeletedBy = "System"; image.DeletedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(photoPath)).StatusCode);
    Assert.Null(Assert.Single((await client.GetFromJsonAsync<PartnerRecordsPage<HotelCatalogEntry>>("/api/hotel-catalog?search=seaside"))!.Items).Photo);
    image.IsDeleted = false; image.DeletedBy = null; image.DeletedAtUtc = null;
    saved.IsDeleted = true; saved.DeletedBy = "System"; saved.DeletedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); await AssertHidden();
  }
}
