using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PartnerDirectoryRequiresBothReviewerRolesAndReturnsAnActualEmptyPage()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string url = "/api/admin/partners";
    var missingPartner = Guid.NewGuid();
    var protectedPaths = new[] { url, $"{url}/{missingPartner}", $"{url}/{missingPartner}/buses",
      $"{url}/{missingPartner}/terminals", $"{url}/{missingPartner}/bookings", $"{url}/{missingPartner}/users" };
    foreach (var token in new string?[] { null, "invalid", factory.Token(resourceAccess: ReviewRoles(), expired: true),
      factory.Token(resourceAccess: ReviewRoles(), trusted: false), factory.Token(resourceAccess: ReviewRoles(), audience: "get-it-done-account-api") })
    {
      client.DefaultRequestHeaders.Authorization = token is null ? null : new("Bearer", token);
      foreach (var path in protectedPaths)
      { using var denied = await client.GetAsync(path); Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode); }
    }
    foreach (var roles in new object[] { new { }, ReviewRoles(admin: false), ReviewRoles(review: false),
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin", "partner.review" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      foreach (var path in protectedPaths)
      { using var denied = await client.GetAsync(path); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); }
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    using var response = await client.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.True(response.Headers.CacheControl!.NoStore);
    var page = (await response.Content.ReadFromJsonAsync<PartnerDirectoryPage>())!;
    Assert.Empty(page.Items); Assert.Empty(page.Categories); Assert.Empty(page.Statuses);
    Assert.Equal(0, page.TotalCount); Assert.Equal(new PartnerDirectorySummary(0, 0, 0), page.Summary);
    foreach (var query in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=51", "page=oops",
      "search=" + new string('x', 255), "category=" + new string('x', 33), "status=" + new string('x', 33) })
    { using var invalid = await client.GetAsync(url + "?" + query); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
    foreach (var kind in new[] { "buses", "terminals", "bookings", "users" })
      foreach (var query in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=51", "page=bad" })
      { using var invalid = await client.GetAsync($"{url}/{missingPartner}/{kind}?{query}"); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); }
  }

  [SqlServerFact]
  public async Task PartnerDirectoryReadsPartnersWithSqlPagingFilteringContactsAndNullableLegacyFields()
  {
    await using var db = CreateDatabase();
    var hotel = ReviewApplication("Hotel application name", "hotel", "Approved", 1, "hotel-owner");
    var bus = ReviewApplication("Bus application name", "bus-operator", "Approved", 2, "bus-owner");
    var pending = ReviewApplication("Pending business", "hotel", "Submitted", 0, "pending-owner");
    db.PartnerApplications.AddRange(hotel, bus, pending);
    var hotelPartner = new Partner { Code = "PT-HOTEL", DisplayName = "Current Hotel", PartnerApplicationId = hotel.Id, OwnerUserId = hotel.CreatedBy };
    var busPartner = new Partner { Code = "PT-BUS", DisplayName = "Current Bus", PartnerApplicationId = bus.Id, Status = "Suspended", OwnerUserId = bus.CreatedBy };
    var demo = new Partner { Code = "DEMO-BUS", DisplayName = "Database Demo Bus", IsDemo = true };
    var legacy = new Partner { Code = "PT-LEGACY", DisplayName = "Legacy business", OwnerUserId = "legacy-owner" };
    var owner = MembershipUser("legacy-owner"); owner.FirstName = "Legacy"; owner.LastName = "Owner"; owner.Email = "legacy@example.com";
    db.RefUsers.Add(owner); db.Partners.AddRange(hotelPartner, busPartner, demo, legacy);
    var joined = DateTimeOffset.UtcNow.AddDays(-2);
    db.PartnerApplicationApprovals.AddRange(
      new PartnerApplicationApproval { Id = Guid.NewGuid(), PartnerApplicationId = hotel.Id, Status = "Approved", ChangedBy = "reviewer", DateTimeCreated = joined.AddDays(1) },
      new PartnerApplicationApproval { Id = Guid.NewGuid(), PartnerApplicationId = bus.Id, Status = "Approved", ChangedBy = "reviewer", DateTimeCreated = joined });
    var location = new Location { Code = "DIRECTORY-MANILA", Name = "Manila" }; db.Locations.Add(location);
    db.Buses.Add(new Bus { PartnerId = demo.Id, FleetNumber = "BUS-1", BusClass = "First Class" });
    db.BusTerminals.Add(new BusTerminal { PartnerId = demo.Id, LocationId = location.Id, Name = "Demo terminal", Address = "Terminal street" });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    const string url = "/api/admin/partners";
    var first = await new EByahe.Infrastructure.Repositories.PartnerDirectoryRepository(db).ListAsync(1, 2, null, null, null, default);
    Assert.Equal(4, first.TotalCount); Assert.Equal(new PartnerDirectorySummary(4, 3, 1), first.Summary);
    Assert.Equal(new[] { hotelPartner.Id, busPartner.Id }, first.Items.Select(x => x.Id));
    Assert.Equal(new[] { "bus-operator", "hotel" }, first.Categories); Assert.Equal(new[] { "Approved", "Suspended" }, first.Statuses);
    var hotelRow = first.Items[0]; Assert.Equal("Current Hotel", hotelRow.BusinessName); Assert.Equal(hotel.Address, hotelRow.Address);
    Assert.Equal(hotel.RepresentativeName, hotelRow.ContactName); Assert.Equal(hotel.RepresentativeEmail, hotelRow.ContactEmail);
    Assert.Equal(joined.AddDays(1), hotelRow.JoinedAtUtc);
    var second = (await client.GetFromJsonAsync<PartnerDirectoryPage>(url + "?page=2&pageSize=2"))!;
    var demoRow = Assert.Single(second.Items, x => x.Id == demo.Id);
    Assert.Equal("bus-operator", demoRow.BusinessCategory); Assert.Equal("Manila", demoRow.Address);
    Assert.Null(demoRow.JoinedAtUtc); Assert.Null(demoRow.ContactName); Assert.Null(demoRow.ContactEmail);
    var legacyRow = Assert.Single(second.Items, x => x.Id == legacy.Id);
    Assert.Null(legacyRow.BusinessCategory); Assert.Null(legacyRow.Address); Assert.Null(legacyRow.JoinedAtUtc);
    Assert.Equal("Legacy Owner", legacyRow.ContactName); Assert.Equal("legacy@example.com", legacyRow.ContactEmail);
    var filtered = (await client.GetFromJsonAsync<PartnerDirectoryPage>(url + "?search=%20current%20&category=hotel&status=Approved"))!;
    Assert.Equal(hotelPartner.Id, Assert.Single(filtered.Items).Id); Assert.Equal(first.Summary, filtered.Summary);
    Assert.Equal(first.Categories, filtered.Categories); Assert.Equal(first.Statuses, filtered.Statuses);
    foreach (var search in new[] { hotelPartner.Code, hotel.RepresentativeName, hotel.RepresentativeEmail, hotel.Address })
      Assert.Contains((await client.GetFromJsonAsync<PartnerDirectoryPage>(url + "?search=" + Uri.EscapeDataString(search)))!.Items, x => x.Id == hotelPartner.Id);
    Assert.Contains((await client.GetFromJsonAsync<PartnerDirectoryPage>(url + "?search=Manila&category=bus-operator"))!.Items, x => x.Id == demo.Id);
    Assert.Equal(legacy.Id, Assert.Single((await client.GetFromJsonAsync<PartnerDirectoryPage>(url + "?search=legacy@example.com"))!.Items).Id);
    Assert.Empty((await client.GetFromJsonAsync<PartnerDirectoryPage>(url + "?search=not-present"))!.Items);
    Assert.Empty((await client.GetFromJsonAsync<PartnerDirectoryPage>(url + "?page=100"))!.Items);
    var body = await client.GetStringAsync(url);
    foreach (var excluded in new[] { "bankName", "accountNumber", "accountHolder", "tin", "storageKey", "documents", "PRIVATE-TAX", "Private Bank", "Pending business" })
      Assert.DoesNotContain(excluded, body);
    Assert.Equal(4, await db.Partners.CountAsync()); // Reading never creates partners from pending applications.
  }

  [SqlServerFact]
  public async Task NewlyApprovedApplicationAppearsInThePartnerDirectory()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm("hotel"); using var submitted = await client.PostAsync("/api/partner-applications", form);
    var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    Assert.Empty((await client.GetFromJsonAsync<PartnerDirectoryPage>("/api/admin/partners"))!.Items);
    using var started = await client.PostAsync($"/api/admin/partner-applications/{receipt.Id}/review", null);
    using var approved = await client.PostAsync($"/api/admin/partner-applications/{receipt.Id}/approve", null);
    Assert.Equal(HttpStatusCode.OK, started.StatusCode); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
    var result = (await client.GetFromJsonAsync<PartnerDirectoryPage>("/api/admin/partners"))!;
    var row = Assert.Single(result.Items); Assert.Equal("Approved", row.Status); Assert.Equal("hotel", row.BusinessCategory);
    Assert.NotNull(row.JoinedAtUtc); Assert.Equal("PT-" + receipt.Id.ToString("N").ToUpperInvariant(), row.Code);
    await using var db = CreateDatabase(); Assert.Equal((await db.Partners.SingleAsync()).Id, row.Id);
  }

  [SqlServerFact]
  public async Task PartnerDetailsReadsExactPartnerWithOwnerApplicationAndPartnerScopedOperations()
  {
    await using var db = CreateDatabase();
    var application = ReviewApplication("Original name", "hotel", "Approved", 1, "detail-owner");
    db.PartnerApplications.Add(application);
    var owner = MembershipUser("detail-owner"); var member = MembershipUser("member");
    var inactive = MembershipUser("inactive"); var outsider = MembershipUser("outsider");
    db.RefUsers.AddRange(owner, member, inactive, outsider);
    var partner = new Partner { Code = "PT-DETAIL", DisplayName = "Current name", Status = "Suspended",
      OwnerUserId = owner.ExternalUserId, PartnerApplicationId = application.Id };
    var other = new Partner { Code = "PT-OTHER", DisplayName = "Other partner" };
    db.Partners.AddRange(partner, other);
    db.PartnerUsers.AddRange(new PartnerUser { PartnerId = partner.Id, RefUserId = owner.Id },
      new PartnerUser { PartnerId = partner.Id, RefUserId = member.Id },
      new PartnerUser { PartnerId = partner.Id, RefUserId = inactive.Id, IsActive = false },
      new PartnerUser { PartnerId = other.Id, RefUserId = outsider.Id });
    db.Buses.AddRange(new Bus { PartnerId = partner.Id, FleetNumber = "FLEET-1", BusClass = "Standard" },
      new Bus { PartnerId = partner.Id, FleetNumber = "FLEET-2", BusClass = "Standard", IsActive = false },
      new Bus { PartnerId = other.Id, FleetNumber = "OTHER-1", BusClass = "Standard" });
    var location = new Location { Code = "DETAIL-LOCATION", Name = "Manila" }; db.Locations.Add(location);
    db.BusTerminals.AddRange(new BusTerminal { PartnerId = partner.Id, LocationId = location.Id, Name = "Main terminal" },
      new BusTerminal { PartnerId = other.Id, LocationId = location.Id, Name = "Other terminal" });
    var joined = DateTimeOffset.UtcNow.AddDays(-1);
    db.PartnerApplicationApprovals.Add(new PartnerApplicationApproval { Id = Guid.NewGuid(), PartnerApplicationId = application.Id,
      Status = "Approved", ChangedBy = "reviewer", DateTimeCreated = joined });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    using var response = await client.GetAsync($"/api/admin/partners/{partner.Id}");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var details = (await response.Content.ReadFromJsonAsync<PartnerDirectoryDetails>())!;
    Assert.Equal(partner.Id, details.Partner.Id); Assert.Equal("Current name", details.Partner.BusinessName);
    Assert.Equal("Suspended", details.Partner.Status); Assert.Equal("hotel", details.Partner.BusinessCategory);
    Assert.Equal(application.Address, details.Partner.Address); Assert.Equal(application.RepresentativeEmail, details.Partner.ContactEmail);
    Assert.Equal(application.RepresentativeName, details.Partner.ContactName); Assert.Equal(joined, details.Partner.JoinedAtUtc);
    Assert.Equal(application.Id, details.ApplicationId); Assert.Equal(application.TrackingId, details.ApplicationTrackingId);
    Assert.Equal(new PartnerOwnerDetails("Partner Member", owner.Email), details.Owner);
    Assert.Equal(2, details.BusCount); Assert.Equal(1, details.TerminalCount); Assert.Equal(2, details.ActiveUserCount);
    var listed = (await client.GetFromJsonAsync<PartnerDirectoryPage>("/api/admin/partners?search=PT-DETAIL"))!;
    Assert.Equal(Assert.Single(listed.Items), details.Partner);
    var body = await response.Content.ReadAsStringAsync();
    foreach (var forbidden in new[] { "accountNumber", "bankName", "tin", "documents", "storageKey", "PRIVATE-TAX", outsider.Email })
      Assert.DoesNotContain(forbidden, body);
    var prefix = $"/api/admin/partners/{partner.Id}";
    using var busesResponse = await client.GetAsync(prefix + "/buses?pageSize=1");
    Assert.True(busesResponse.Headers.CacheControl!.NoStore);
    var buses = (await busesResponse.Content.ReadFromJsonAsync<PartnerRecordsPage<PartnerBusRecord>>())!;
    Assert.Equal(details.BusCount, buses.TotalCount); Assert.Equal("FLEET-1", Assert.Single(buses.Items).FleetNumber);
    var nextBuses = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerBusRecord>>(prefix + "/buses?page=2&pageSize=1"))!;
    Assert.Equal("FLEET-2", Assert.Single(nextBuses.Items).FleetNumber); Assert.False(nextBuses.Items[0].IsActive);
    var terminals = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerTerminalRecord>>(prefix + "/terminals"))!;
    Assert.Equal(details.TerminalCount, terminals.TotalCount); Assert.Equal("Main terminal", Assert.Single(terminals.Items).Name);
    Assert.Equal("Manila", terminals.Items[0].Location);
    var users = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerUserRecord>>(prefix + "/users?pageSize=1"))!;
    var nextUsers = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerUserRecord>>(prefix + "/users?page=2&pageSize=1"))!;
    Assert.Equal(details.ActiveUserCount, users.TotalCount); Assert.Single(users.Items); Assert.Single(nextUsers.Items);
    Assert.Equal(new[] { owner.Email, member.Email }.Order(), users.Items.Concat(nextUsers.Items).Select(x => x.Email).Order());
    Assert.NotEqual(users.Items[0].Id, nextUsers.Items[0].Id);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerBusRecord>>(prefix + "/buses?page=100"))!.Items);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerUserRecord>>(prefix + "/users?page=100"))!.Items);
  }

  [SqlServerFact]
  public async Task PartnerDetailsPreservesLegacyRecordsAndReturnsNotFoundForMissingOrInvalidIds()
  {
    await using var db = CreateDatabase();
    var partner = new Partner { Code = "LEGACY-DETAIL", DisplayName = "Legacy partner" };
    db.Partners.Add(partner); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    var details = (await client.GetFromJsonAsync<PartnerDirectoryDetails>($"/api/admin/partners/{partner.Id}"))!;
    Assert.Equal(partner.Id, details.Partner.Id); Assert.Null(details.Owner); Assert.Null(details.ApplicationId);
    Assert.Null(details.ApplicationTrackingId); Assert.Null(details.Partner.BusinessCategory); Assert.Null(details.Partner.JoinedAtUtc);
    Assert.Null(details.Partner.Address); Assert.Null(details.Partner.ContactName); Assert.Null(details.Partner.ContactEmail);
    Assert.Equal(0, details.BusCount); Assert.Equal(0, details.TerminalCount); Assert.Equal(0, details.ActiveUserCount); Assert.Equal(0, details.BookingCount);
    foreach (var id in new[] { Guid.NewGuid().ToString(), "invalid-id" })
    { using var missing = await client.GetAsync("/api/admin/partners/" + id); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); }
    foreach (var kind in new[] { "buses", "terminals", "bookings", "users" })
    {
      using var empty = await client.GetAsync($"/api/admin/partners/{partner.Id}/{kind}");
      Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
      var records = (await empty.Content.ReadFromJsonAsync<PartnerRecordsPage<System.Text.Json.JsonElement>>())!;
      Assert.Empty(records.Items); Assert.Equal(0, records.TotalCount); Assert.Equal(1, records.Page); Assert.Equal(10, records.PageSize);
      using var missing = await client.GetAsync($"/api/admin/partners/{Guid.NewGuid()}/{kind}"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
  }

  [SqlServerFact]
  public async Task PartnerBookingCountUsesScheduledTripOwnershipAcrossDeparturesAndBookers()
  {
    await SeedBookingTripAsync();
    await using var db = CreateDatabase();
    var first = await db.ScheduledTrips.SingleAsync();
    var second = new ScheduledTrip { PartnerId = first.PartnerId, BusId = first.BusId, BusRouteId = first.BusRouteId,
      DepartureAtUtc = first.DepartureAtUtc.AddDays(1), BaseFare = first.BaseFare, AvailableSeats = 40, Status = "Cancelled" };
    var other = new Partner { Code = "OTHER-BOOKINGS", DisplayName = "Other operator" };
    var origin = new BusTerminal { Partner = other, Location = new Location { Code = "OTHER-FROM", Name = "Other origin" }, Name = "Origin terminal" };
    var destination = new BusTerminal { Partner = other, Location = new Location { Code = "OTHER-TO", Name = "Other destination" }, Name = "Destination terminal" };
    var otherTrip = new ScheduledTrip { Partner = other,
      Bus = new Bus { Partner = other, FleetNumber = "OTHER-1", BusClass = "Standard" },
      BusRoute = new BusRoute { Partner = other, OriginTerminal = origin, DestinationTerminal = destination, DurationMinutes = 60 },
      DepartureAtUtc = first.DepartureAtUtc, BaseFare = 100, AvailableSeats = 40 };
    db.ScheduledTrips.AddRange(second, otherTrip);
    static BusBooking Booking(Guid? trip, string booker, int seats) => new() {
      Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), ScheduledTripId = trip, CreatedBy = booker,
      Reference = "BK-" + Guid.NewGuid().ToString("N"), CreatedAtUtc = DateTimeOffset.UtcNow,
      ReservedSeats = seats, RequestHash = new string('A', 64), DetailsJson = "{}"
    };
    db.BusBookings.AddRange(Booking(first.Id, "shared-booker", 1), Booking(second.Id, "another-booker", 3),
      Booking(otherTrip.Id, "shared-booker", 1), Booking(null, "shared-booker", 0));
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: ReviewRoles()));
    var details = (await client.GetFromJsonAsync<PartnerDirectoryDetails>($"/api/admin/partners/{first.PartnerId}"))!;
    Assert.Equal(2, details.BookingCount); // Bookings, not the four reserved passenger seats.
    var otherDetails = (await client.GetFromJsonAsync<PartnerDirectoryDetails>($"/api/admin/partners/{other.Id}"))!;
    Assert.Equal(1, otherDetails.BookingCount);
    Assert.Equal(4, await db.BusBookings.CountAsync()); // Legacy records without a trip cannot be attributed.
    var prefix = $"/api/admin/partners/{first.PartnerId}/bookings";
    var bookings = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerBookingRecord>>(prefix + "?pageSize=1"))!;
    var nextBookings = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerBookingRecord>>(prefix + "?page=2&pageSize=1"))!;
    Assert.Equal(details.BookingCount, bookings.TotalCount); Assert.Single(bookings.Items); Assert.Single(nextBookings.Items);
    Assert.NotEqual(bookings.Items[0].Id, nextBookings.Items[0].Id);
    Assert.Equal(new[] { 1, 3 }, bookings.Items.Concat(nextBookings.Items).Select(x => x.ReservedSeats).Order());
    Assert.All(bookings.Items.Concat(nextBookings.Items), x => { Assert.Equal("Manila", x.Origin); Assert.Equal("Baguio", x.Destination); Assert.Equal("TST-1234", x.PlateNumber); });
    var json = await client.GetStringAsync(prefix);
    foreach (var privateField in new[] { "createdBy", "detailsJson", "requestHash", "shared-booker", "another-booker", "accountNumber", "passengers", "contact" })
      Assert.DoesNotContain(privateField, json);
    Assert.Empty((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerBookingRecord>>(prefix + "?page=3&pageSize=1"))!.Items);
  }
}
