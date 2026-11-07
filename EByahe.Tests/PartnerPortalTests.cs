using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static object PartnerRoles() => new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "partner" } } };

  [SqlServerFact]
  public async Task PartnerPortalRequiresPartnerRoleInsteadOfAdminAcrossEveryEndpoint()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var paths = new[] { "dashboard", "bookings", "terminals", "scheduled-trips", "users" };
    foreach (var token in new string?[] { null, "invalid", factory.Token(resourceAccess: PartnerRoles(), expired: true), factory.Token(resourceAccess: PartnerRoles(), trusted: false) })
    {
      client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);
      foreach (var path in paths) { using var denied = await client.GetAsync("/api/partner/" + path); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); }
    }
    foreach (var roles in new object[] { ReviewRoles(), BookingRoles(),
      new Dictionary<string, object> { ["get-it-done-ebyahe-api"] = new { roles = new[] { "partner" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles, realmAccess: new { roles = new[] { "partner" } }));
      foreach (var path in paths) { using var denied = await client.GetAsync("/api/partner/" + path); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: PartnerRoles()));
    foreach (var path in paths)
    {
      using var empty = await client.GetAsync("/api/partner/" + path); Assert.Equal(HttpStatusCode.OK, empty.StatusCode); Assert.True(empty.Headers.CacheControl!.NoStore);
    }
    Assert.Equal(new PartnerDashboard(0, 0, 0, 0, 0), await client.GetFromJsonAsync<PartnerDashboard>("/api/partner/dashboard"));
    foreach (var kind in paths.Skip(1)) foreach (var query in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=51" })
    { using var invalid = await client.GetAsync($"/api/partner/{kind}?{query}"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
  }

  [SqlServerFact]
  public async Task PartnerPortalReadsOnlyApprovedActiveMembershipsWithDatabasePagination()
  {
    await SeedBookingTripAsync();
    await using var db = CreateDatabase();
    var first = await db.ScheduledTrips.SingleAsync();
    var member = MembershipUser("portal-member"); var colleague = MembershipUser("portal-colleague");
    var inactive = MembershipUser("portal-inactive"); db.RefUsers.AddRange(member, colleague, inactive);
    var membership = new PartnerUser { PartnerId = first.PartnerId, RefUserId = member.Id };
    db.PartnerUsers.AddRange(membership, new PartnerUser { PartnerId = first.PartnerId, RefUserId = colleague.Id },
      new PartnerUser { PartnerId = first.PartnerId, RefUserId = inactive.Id, IsActive = false });
    var second = new ScheduledTrip { PartnerId = first.PartnerId, BusId = first.BusId, BusRouteId = first.BusRouteId,
      DepartureAtUtc = first.DepartureAtUtc.AddDays(1), BaseFare = 800, AvailableSeats = 12 };
    var foreign = new Partner { Code = "FOREIGN-PORTAL", DisplayName = "Foreign operator" };
    var origin = new BusTerminal { Partner = foreign, Location = new Location { Code = "PORTAL-FROM", Name = "Hidden origin" }, Name = "Hidden terminal" };
    var destination = new BusTerminal { Partner = foreign, Location = new Location { Code = "PORTAL-TO", Name = "Hidden destination" }, Name = "Hidden destination terminal" };
    var foreignTrip = new ScheduledTrip { Partner = foreign, Bus = new Bus { Partner = foreign, FleetNumber = "HIDDEN", BusClass = "Hidden class" },
      BusRoute = new BusRoute { Partner = foreign, OriginTerminal = origin, DestinationTerminal = destination, DurationMinutes = 60 }, DepartureAtUtc = first.DepartureAtUtc, AvailableSeats = 20, BaseFare = 100 };
    db.ScheduledTrips.AddRange(second, foreignTrip);
    db.PartnerUsers.Add(new PartnerUser { Partner = foreign, RefUserId = colleague.Id });
    static BusBooking Booking(Guid? trip) => new() { Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), ScheduledTripId = trip,
      Reference = "BK-" + Guid.NewGuid().ToString("N"), CreatedBy = "customer", CreatedAtUtc = DateTimeOffset.UtcNow, RequestHash = new string('A', 64), DetailsJson = "{}", ReservedSeats = trip == null ? 0 : 1 };
    db.BusBookings.AddRange(Booking(first.Id), Booking(second.Id), Booking(foreignTrip.Id), Booking(null)); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "portal-member", resourceAccess: PartnerRoles()));
    var dashboard = await client.GetFromJsonAsync<PartnerDashboard>("/api/partner/dashboard"); Assert.Equal(new PartnerDashboard(1, 2, 2, 2, 2), dashboard);
    var terminals = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTerminal>>("/api/partner/terminals?pageSize=1"))!;
    var nextTerminal = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTerminal>>("/api/partner/terminals?pageSize=1&page=2"))!;
    Assert.Equal(2, terminals.TotalCount); Assert.Single(terminals.Items); Assert.Single(nextTerminal.Items); Assert.NotEqual(terminals.Items[0].Id, nextTerminal.Items[0].Id);
    var trips = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalTrip>>("/api/partner/scheduled-trips"))!;
    Assert.Equal(2, trips.TotalCount); Assert.All(trips.Items, row => { Assert.Equal("Victory Liner", row.Partner); Assert.Equal("TST-1234", row.PlateNumber); });
    var users = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalUser>>("/api/partner/users"))!;
    Assert.Equal(3, users.TotalCount); Assert.Contains(users.Items, row => !row.IsActive);
    using var bookingsResponse = await client.GetAsync("/api/partner/bookings?pageSize=1");
    var bookings = (await bookingsResponse.Content.ReadFromJsonAsync<PartnerRecordsPage<AdminBusBookingSummary>>())!;
    Assert.Equal(2, bookings.TotalCount); Assert.Single(bookings.Items);
    var json = await bookingsResponse.Content.ReadAsStringAsync(); Assert.DoesNotContain("detailsJson", json); Assert.DoesNotContain("createdBy", json);
    foreach (var user in new[] { "PORTAL-MEMBER", "stranger", "portal-inactive" })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: user, resourceAccess: PartnerRoles()));
      Assert.Equal(new PartnerDashboard(0, 0, 0, 0, 0), await client.GetFromJsonAsync<PartnerDashboard>("/api/partner/dashboard?userId=portal-member&partnerId=" + first.PartnerId));
    }
    membership.IsActive = false; await db.SaveChangesAsync();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "portal-member", resourceAccess: PartnerRoles()));
    Assert.Equal(new PartnerDashboard(0, 0, 0, 0, 0), await client.GetFromJsonAsync<PartnerDashboard>("/api/partner/dashboard"));
    membership.IsActive = true; var partner = await db.Partners.FindAsync(first.PartnerId); partner!.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalUser>>("/api/partner/users"))!.Items);
  }
}
