using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static object BookingRoles(bool admin = true, bool read = true, bool review = false) => new Dictionary<string, object>
  {
    ["get-it-done-web"] = new { roles = admin ? new[] { "admin" } : [] },
    ["get-it-done-ebyahe-api"] = new { roles = (read ? new[] { "booking.read" } : []).Concat(review ? ["partner.review"] : []).ToArray() }
  };

  [SqlServerFact]
  public async Task AdminBusBookingListRequiresExactAdminAndBookingReadRolesAndValidPaging()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string url = "/api/admin/bus-bookings";
    foreach (var token in new string?[] { null, "invalid", factory.Token(resourceAccess: BookingRoles(), expired: true),
      factory.Token(resourceAccess: BookingRoles(), trusted: false), factory.Token(resourceAccess: BookingRoles(), audience: "get-it-done-account-api") })
    {
      client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);
      using var response = await client.GetAsync(url); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    foreach (var roles in new object[] { new { }, BookingRoles(admin: false), BookingRoles(read: false), ReviewRoles(),
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin", "booking.read" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      using var response = await client.GetAsync(url); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BookingRoles()));
    using var emptyResponse = await client.GetAsync(url);
    Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
    Assert.True(emptyResponse.Headers.CacheControl!.NoStore); Assert.True(emptyResponse.Headers.CacheControl.Private);
    var empty = (await emptyResponse.Content.ReadFromJsonAsync<AdminBusBookingsResponse>())!;
    Assert.Empty(empty.Items); Assert.Empty(empty.Statuses); Assert.Equal(0, empty.TotalCount);
    foreach (var query in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=51", "page=invalid",
      "search=" + new string('x', 65), "status=" + new string('x', 33) })
    { using var invalid = await client.GetAsync(url + "?" + query); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
  }

  [SqlServerFact]
  public async Task AdminBusBookingListUsesSqlPagingAndFilteringWithoutExposingPrivateSnapshots()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var created = new List<BusBookingResponse>();
    for (var index = 0; index < 3; index++)
    {
      using var response = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest());
      Assert.Equal(HttpStatusCode.Created, response.StatusCode);
      created.Add((await response.Content.ReadFromJsonAsync<BusBookingResponse>())!);
    }
    await using var database = CreateDatabase();
    var trip = await database.ScheduledTrips.Include(row => row.Partner).SingleAsync(row => row.Id == TestTripId);
    trip.Partner.DisplayName = "Renamed operator after booking"; await database.SaveChangesAsync();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "platform-reviewer", resourceAccess: BookingRoles(review: true)));
    const string url = "/api/admin/bus-bookings";
    using var firstResponse = await client.GetAsync(url + "?page=1&pageSize=2");
    Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
    var first = (await firstResponse.Content.ReadFromJsonAsync<AdminBusBookingsResponse>())!;
    Assert.Equal(3, first.TotalCount); Assert.Equal(2, first.Items.Count); Assert.Equal(1, first.Page); Assert.Equal(2, first.PageSize);
    Assert.Equal(created.AsEnumerable().Reverse().Take(2).Select(row => row.Id), first.Items.Select(row => row.Id));
    Assert.All(first.Items, row => { Assert.Equal("Manila", row.Origin); Assert.Equal("Baguio", row.Destination);
      Assert.Equal("Victory Liner", row.Operator); Assert.Equal(1, row.Passengers); Assert.Equal(1, row.ReservedSeats); });
    var second = (await client.GetFromJsonAsync<AdminBusBookingsResponse>(url + "?page=2&pageSize=2"))!;
    Assert.Equal(created[0].Id, Assert.Single(second.Items).Id);
    var filtered = (await client.GetFromJsonAsync<AdminBusBookingsResponse>(url + "?search=" + created[0].Reference + "&status=Submitted"))!;
    Assert.Equal(created[0].Id, Assert.Single(filtered.Items).Id); Assert.Equal(new[] { "Submitted" }, filtered.Statuses);
    var noMatch = (await client.GetFromJsonAsync<AdminBusBookingsResponse>(url + "?search=not-found"))!;
    Assert.Empty(noMatch.Items); Assert.Equal(0, noMatch.TotalCount);
    var json = await firstResponse.Content.ReadAsStringAsync();
    foreach (var value in new[] { "detailsJson", "createdBy", "requestHash", "requestId", "booker@example.com", "Test Traveler", "cardholder", "last4" })
      Assert.DoesNotContain(value, json, StringComparison.OrdinalIgnoreCase);
  }

  [SqlServerFact]
  public async Task PartnerBookingAccessUsesActiveMembershipsAndNeverACallerSuppliedPartnerOrUser()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var booking = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest());
    Assert.Equal(HttpStatusCode.Created, booking.StatusCode);
    var receipt = (await booking.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    await using var database = CreateDatabase();
    var trip = await database.ScheduledTrips.SingleAsync(row => row.Id == TestTripId);
    var member = MembershipUser("partner-member"); database.RefUsers.Add(member);
    var membership = new PartnerUser { PartnerId = trip.PartnerId, RefUserId = member.Id };
    database.PartnerUsers.Add(membership);
    var legacy = new BusBooking { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), CreatedBy = "another-customer", Reference = "BK-LEGACY-ADMIN",
      CreatedAtUtc = DateTimeOffset.UtcNow, RequestHash = new string('A', 64), DetailsJson = "{}", Total = 100m };
    database.BusBookings.Add(legacy); await database.SaveChangesAsync();
    const string url = "/api/admin/bus-bookings";
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "partner-member", resourceAccess: BookingRoles()));
    var own = (await client.GetFromJsonAsync<AdminBusBookingsResponse>(url + "?partnerId=" + Guid.NewGuid()))!;
    Assert.Equal(receipt.Id, Assert.Single(own.Items).Id);
    foreach (var subject in new[] { "unrelated-admin", "PARTNER-MEMBER", PartnerApiFactory.Subject })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: subject, resourceAccess: BookingRoles()));
      var denied = (await client.GetFromJsonAsync<AdminBusBookingsResponse>(url + "?userId=partner-member&partnerId=" + trip.PartnerId))!;
      Assert.Empty(denied.Items); Assert.Empty(denied.Statuses);
    }
    membership.IsActive = false; await database.SaveChangesAsync();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "partner-member", resourceAccess: BookingRoles()));
    Assert.Empty((await client.GetFromJsonAsync<AdminBusBookingsResponse>(url))!.Items);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: BookingRoles(review: true)));
    var all = (await client.GetFromJsonAsync<AdminBusBookingsResponse>(url))!;
    Assert.Equal(2, all.TotalCount); Assert.Contains(all.Items, row => row.Id == legacy.Id && row.Origin == null);
  }
}
