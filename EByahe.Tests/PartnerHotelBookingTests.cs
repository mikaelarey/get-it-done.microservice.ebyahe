using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PartnerHotelBookingsScopeReservationsSearchAndDetailsToActiveHotelCompanies()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    var created = await client.PostAsJsonAsync("/api/partner/hotels", command);
    var hotel = (await created.Content.ReadFromJsonAsync<PartnerHotel>())!;
    await using var db = CreateDatabase();
    var reservation = new HotelBooking { HotelId = hotel.Id, Reference = "HB-SEASIDE-001", GuestName = "Maria Santos",
      GuestEmail = "maria@example.com", GuestPhone = "09171234567", RoomName = "Deluxe sea view", RoomCount = 2, GuestCount = 3,
      CheckIn = new(2035, 6, 1), CheckOut = new(2035, 6, 4), Total = 6500.50m, CodeDecodePaymentStatus = 3,
      CreatedBy = PartnerApiFactory.Subject, CreatedAtUtc = DateTimeOffset.UtcNow };
    db.HotelBookings.AddRange(reservation, new HotelBooking { HotelId = hotel.Id, Reference = "HB-SEASIDE-002", GuestName = "Juan Cruz",
      GuestEmail = "juan@example.com", RoomName = "Standard room", CheckIn = new(2035, 6, 5), CheckOut = new(2035, 6, 6),
      Total = 1000, CreatedBy = PartnerApiFactory.Subject, CreatedAtUtc = reservation.CreatedAtUtc.AddMinutes(-1) });
    var foreign = new Partner { Code = "FOREIGN-HOTEL", DisplayName = "Foreign company" };
    var foreignHotel = new Hotel { Partner = foreign, Name = "Private Hotel", Address = command.Address, Email = command.Email,
      ContactNumber = command.ContactNumber, CodeDecodeRegion = command.CodeDecodeRegion, CodeDecodeProvince = command.CodeDecodeProvince,
      CodeDecodeMunicipality = command.CodeDecodeMunicipality, CodeDecodeBarangay = command.CodeDecodeBarangay };
    var foreignReservation = new HotelBooking { Hotel = foreignHotel, Reference = "HB-PRIVATE", GuestName = "Private Guest", RoomName = "Private room",
      CheckIn = new(2035, 6, 1), CheckOut = new(2035, 6, 2), Total = 2000 };
    db.HotelBookings.Add(foreignReservation); await db.SaveChangesAsync();
    // Reservation viewing requires booking.read; hotel.read and hotel.write are not needed.
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("booking.read")));
    using var response = await client.GetAsync("/api/partner/hotel-bookings?pageSize=1");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var page = (await response.Content.ReadFromJsonAsync<PartnerRecordsPage<PartnerHotelBooking>>())!;
    Assert.Equal(2, page.TotalCount); var first = Assert.Single(page.Items); Assert.Equal(reservation.Id, first.Id);
    Assert.Equal(3, first.Nights); Assert.Equal(2, first.RoomCount); Assert.Equal(6500.50m, first.Total);
    Assert.Equal("Payment Success", first.PaymentStatus); Assert.Contains(command.Address, first.HotelAddress);
    var second = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotelBooking>>("/api/partner/hotel-bookings?pageSize=1&page=2"))!;
    Assert.NotEqual(first.Id, Assert.Single(second.Items).Id);
    var search = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotelBooking>>("/api/partner/hotel-bookings?search=maria%20deluxe"))!;
    Assert.Equal(first.Id, Assert.Single(search.Items).Id);
    Assert.Equal(first, await client.GetFromJsonAsync<PartnerHotelBooking>("/api/partner/hotel-bookings/" + first.Id));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/partner/hotel-bookings/" + foreignReservation.Id + "?partnerId=" + foreign.Id)).StatusCode);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/partner/hotel-bookings?page=0")).StatusCode);
    // Removing a property from the catalog preserves access to its existing reservations.
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/partner/hotels/" + hotel.Id)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("booking.read")));
    Assert.Equal(first.Id, (await client.GetFromJsonAsync<PartnerHotelBooking>("/api/partner/hotel-bookings/" + first.Id))!.Id);
    var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotelBooking>>("/api/partner/hotel-bookings"))!.Items);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/partner/hotel-bookings/" + first.Id)).StatusCode);
    member.IsActive = true; var application = await db.PartnerApplications.SingleAsync(); application.Status = "Rejected"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotelBooking>>("/api/partner/hotel-bookings"))!.Items);
  }

  [SqlServerFact]
  public async Task PartnerBookingTypesFollowApprovedCompanyMembershipsAndExactBookingReadPermission()
  {
    var hotelPartner = await SeedMemberPartner("hotel");
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var paths = new[] { "/api/partner/bookings/types", "/api/partner/hotel-bookings", "/api/partner/hotel-bookings/" + Guid.NewGuid() };
    client.DefaultRequestHeaders.Authorization = null;
    foreach (var path in paths) Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    foreach (var roles in new[] { BusRoles("hotel.read", "room.read"), BusRoles("booking.confirm"), BookingRoles(),
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin" } },
        ["get-it-done-ebyahe-api"] = new { roles = new[] { "booking.read" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      foreach (var path in paths) Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("booking.read")));
    Assert.Equal(["hotel"], (await client.GetFromJsonAsync<PartnerBookingTypes>(paths[0]))!.Types);
    await using var db = CreateDatabase(); var user = await db.RefUsers.SingleAsync(row => row.ExternalUserId == PartnerApiFactory.Subject);
    var application = new PartnerApplication { Id = Guid.NewGuid(), TrackingId = "APP-BUS-TYPE", CreatedBy = PartnerApiFactory.Subject,
      Status = "Approved", BusinessCategory = "bus-operator", BusinessName = "Bus company", EntityType = "corporation", SettlementFrequency = "daily" };
    var bus = new Partner { Code = "BUS-TYPE", DisplayName = "Bus company", PartnerApplicationId = application.Id };
    var busMember = new PartnerUser { Partner = bus, RefUserId = user.Id };
    db.PartnerApplications.Add(application); db.PartnerUsers.Add(busMember); await db.SaveChangesAsync();
    Assert.Equal(["bus", "hotel"], (await client.GetFromJsonAsync<PartnerBookingTypes>(paths[0]))!.Types);
    var hotelMember = await db.PartnerUsers.SingleAsync(row => row.PartnerId == hotelPartner.Id);
    hotelMember.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(["bus"], (await client.GetFromJsonAsync<PartnerBookingTypes>(paths[0]))!.Types);
    bus.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerBookingTypes>(paths[0]))!.Types);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "another-person", resourceAccess: BusRoles("booking.read")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerBookingTypes>(paths[0]))!.Types);
    Assert.False(db.Database.HasPendingModelChanges());
  }
}
