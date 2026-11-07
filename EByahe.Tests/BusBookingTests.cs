using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using EByahe.Application.Dtos.Requests;
using EByahe.Application.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static readonly Guid TestTripId = Guid.Parse("10000000-0000-4000-8000-000000000001");
  private static readonly Guid MissingTripId = Guid.Parse("10000000-0000-4000-8000-000000000999");

  private static CreateBusBookingRequest BookingRequest() => new()
  {
    RequestId = Guid.NewGuid(),
    Trip = new() { TripId = TestTripId, Origin = "Manila", Destination = "Baguio", Operator = "Victory Liner", BusClass = "Deluxe",
      DepartureDate = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd"), DepartureTime = "06:00", TravelTime = "6 hrs" },
    Booking = new()
    {
      Boarding = "Manila Bus Station", Dropping = "Baguio Bus Station",
      Passengers = [new() { FullName = "Test Traveler", Age = null, Type = "adult", Gender = "", Request = "Window" }],
      Contact = new() { FullName = "Test Booker", Email = "booker@example.com", Phone = "+63 917 123 4567", EmergencyPhone = "", Updates = true }
    },
    Payment = new() { Method = "cash", Provider = "Bus station" },
    Fare = new() { Fares = [new() { Label = "12A · Adult", Amount = 750m }], Fee = 22.5m, Total = 772.5m }
  };

  [SqlServerFact]
  public async Task BusBookingListsAreDatabaseBackedPaginatedAndOwnedByTheToken()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var created = new List<BusBookingResponse>();
    for (var index = 0; index < 3; index++)
    {
      using var response = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest());
      Assert.Equal(HttpStatusCode.Created, response.StatusCode);
      created.Add((await response.Content.ReadFromJsonAsync<BusBookingResponse>())!);
    }
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "another-owner"));
    using (var other = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest())) Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token());
    using var list = await client.GetAsync("/api/bus-bookings?page=1&pageSize=2&userId=another-owner");
    Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    Assert.True(list.Headers.CacheControl!.Private);
    Assert.True(list.Headers.CacheControl.NoStore);
    var page = (await list.Content.ReadFromJsonAsync<BusBookingPage>())!;
    Assert.Equal(3, page.TotalCount);
    Assert.Equal(1, page.Page); Assert.Equal(2, page.PageSize);
    Assert.Equal(created.AsEnumerable().Reverse().Take(2).Select(row => row.Reference), page.Items.Select(row => row.Reference));
    Assert.All(page.Items, row => { Assert.Equal("Manila", row.Trip.Origin); Assert.Equal(1, row.Passengers); Assert.Equal("Bus station", row.PaymentMethod); });
    var json = await list.Content.ReadAsStringAsync();
    foreach (var privateField in new[] { "booker@example.com", "Test Traveler", "createdBy", "requestHash", "requestId", "detailsJson" })
      Assert.DoesNotContain(privateField, json);
    var second = (await client.GetFromJsonAsync<BusBookingPage>("/api/bus-bookings?page=2&pageSize=2"))!;
    Assert.Equal(created[0].Reference, Assert.Single(second.Items).Reference);
    var empty = (await client.GetFromJsonAsync<BusBookingPage>("/api/bus-bookings?page=3&pageSize=2"))!;
    Assert.Empty(empty.Items); Assert.Equal(3, empty.TotalCount);
    foreach (var query in new[] { "page=0", "page=-1", "pageSize=0", "pageSize=51" })
    {
      using var invalid = await client.GetAsync("/api/bus-bookings?" + query);
      Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
  }

  [SqlServerFact]
  public async Task BusBookingDetailsLoadStoredSnapshotsAndRejectGuestsAndOtherOwners()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var request = BookingRequest();
    using var created = await client.PostAsJsonAsync("/api/bus-bookings", request);
    var receipt = (await created.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    var path = $"/api/bus-bookings/by-reference/{receipt.Reference}";
    using var response = await client.GetAsync(path);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var details = (await response.Content.ReadFromJsonAsync<BusBookingDetailsResponse>())!;
    Assert.Equal(receipt.Id, details.Id);
    Assert.Equal("booker@example.com", details.Booking.Contact.Email);
    Assert.Null(Assert.Single(details.Booking.Passengers).Age);
    Assert.Equal(request.Booking.Boarding, details.Booking.Boarding);
    Assert.Equal(request.Fare.Total, details.Fare.Total);
    Assert.Equal("cash", details.Payment.Method);
    var json = await response.Content.ReadAsStringAsync();
    foreach (var field in new[] { "createdBy", "requestHash", "requestId", "detailsJson" }) Assert.DoesNotContain(field, json);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "another-owner"));
    using var forbidden = await client.GetAsync(path);
    Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    var empty = (await client.GetFromJsonAsync<BusBookingPage>("/api/bus-bookings"))!;
    Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalCount);
    client.DefaultRequestHeaders.Authorization = null;
    foreach (var route in new[] { "/api/bus-bookings", path, $"/api/bus-bookings/{receipt.Id}" })
    {
      using var anonymous = await client.GetAsync(route);
      Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
  }

  [SqlServerFact]
  public async Task BusBookingsRequireValidAuthenticatedAccessTokensBeforeSaving()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    foreach (var token in new string?[] { null, "invalid", factory.Token(expired: true), factory.Token(trusted: false),
      factory.Token(audience: "get-it-done-account-api"), factory.Token(subject: null), factory.Token(type: "ID") })
    {
      client.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);
      using var response = await client.PostAsJsonAsync("/api/bus-bookings", BookingRequest());
      Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    await using var database = CreateDatabase();
    Assert.Empty(await database.BusBookings.ToListAsync());
  }

  [SqlServerFact]
  public async Task BusBookingsPersistOptionalAgesAndTakeOwnershipFromTheToken()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var request = BookingRequest();
    using var response = await client.PostAsJsonAsync("/api/bus-bookings", request);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    var receipt = (await response.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    Assert.Matches("^BK-[A-F0-9]{32}$", receipt.Reference);
    Assert.Equal("Submitted", receipt.Status);
    Assert.Equal(1, receipt.ReservedSeats);
    Assert.Equal(772.5m, receipt.Total);
    await using var database = CreateDatabase();
    var saved = await database.BusBookings.SingleAsync();
    Assert.Equal(PartnerApiFactory.Subject, saved.CreatedBy);
    Assert.Equal(receipt.Id, saved.Id);
    Assert.Equal(request.RequestId, saved.RequestId);
    using var details = JsonDocument.Parse(saved.DetailsJson);
    Assert.Equal(JsonValueKind.Null, details.RootElement.GetProperty("booking").GetProperty("passengers")[0].GetProperty("age").ValueKind);
    Assert.Equal("booker@example.com", details.RootElement.GetProperty("booking").GetProperty("contact").GetProperty("email").GetString());
    Assert.Equal("Manila", details.RootElement.GetProperty("trip").GetProperty("origin").GetString());
    Assert.True(details.RootElement.GetProperty("booking").GetProperty("contact").GetProperty("updates").GetBoolean());
    using var own = await client.GetAsync(response.Headers.Location);
    Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    Assert.True(own.Headers.CacheControl!.NoStore);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: PartnerApiFactory.Subject.ToUpperInvariant()));
    using var other = await client.GetAsync(response.Headers.Location);
    Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
  }

  [SqlServerFact]
  public async Task BusBookingRetriesAreIdempotentAndRejectChangedPayloads()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var request = BookingRequest();
    var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync("/api/bus-bookings", request)));
    try
    {
      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
      Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
      var first = (await responses[0].Content.ReadFromJsonAsync<BusBookingResponse>())!;
      var second = (await responses[1].Content.ReadFromJsonAsync<BusBookingResponse>())!;
      Assert.Equal(first, second);
      request.Booking.Passengers[0].FullName = "Different traveler";
      using var conflict = await client.PostAsJsonAsync("/api/bus-bookings", request);
      Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
      await using var database = CreateDatabase();
      Assert.Single(await database.BusBookings.ToListAsync());
      Assert.Equal(39, (await database.ScheduledTrips.SingleAsync()).AvailableSeats);
    }
    finally { foreach (var response in responses) response.Dispose(); }
  }

  [SqlServerFact]
  public async Task BusBookingsRejectInvalidPassengersFaresAndSensitiveOrSpoofedFields()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var invalid = new Action<JsonNode>[]
    {
      node => node["requestId"] = Guid.Empty,
      node => node["createdBy"] = "another-user",
      node => node["booking"]!["passengers"]![0]!["age"] = -1,
      node => node["booking"]!["passengers"]![0]!["age"] = 121,
      node => node["booking"]!["passengers"]![0]!["age"] = 1.5,
      node => node["booking"]!["passengers"]![0] = null,
      node => node["booking"]!["passengers"] = new JsonArray(),
      node => node["booking"]!["contact"]!["phone"] = "123",
      node => node["booking"]!["contact"]!["email"] = "not-email",
      node => node["trip"]!["departureDate"] = "2026-02-30",
      node => node["trip"]!["departureTime"] = "25:00",
      node => node["fare"]!["total"] = 1,
      node => node["fare"]!["fares"]![0]!["amount"] = 750.001,
      node => node["fare"]!["fee"] = -1,
      node => node["payment"]!["number"] = "4242424242424242",
      node => node["payment"]!["cvv"] = "123",
      node => node["payment"]!["provider"] = "Unsupported provider"
    };
    foreach (var change in invalid)
    {
      var node = JsonSerializer.SerializeToNode(BookingRequest(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
      change(node);
      using var response = await client.PostAsJsonAsync("/api/bus-bookings", node);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    await using var database = CreateDatabase();
    Assert.Empty(await database.BusBookings.ToListAsync());
  }
}
