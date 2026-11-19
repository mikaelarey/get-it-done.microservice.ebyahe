using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PartnerKeywordSearchCoversVisibleColumnsBeforePaginationAndExcludesStatusesHiddenFieldsAndOtherPartners()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var savedBus = await db.Buses.Include(row => row.BusClass).SingleAsync(); savedBus.FleetNumber = "SEARCH-001"; savedBus.PlateNumber = "KEY-1234"; savedBus.SeatCapacity = 37;
    var terminal = await db.BusTerminals.Include(row => row.Location).SingleAsync(row => row.Id == origin.Id);
    terminal.Name = "Search terminal"; terminal.Address = "100 Aurora Avenue";
    terminal.Location.CodeDecodeRegion = 3; terminal.Location.CodeDecodeProvince = 2; terminal.Location.CodeDecodeMunicipality = 1; terminal.Location.CodeDecodeBarangay = 1;
    var user = MembershipUser("search-target"); user.FirstName = "Maria"; user.LastName = "Santos"; user.Email = "maria%keyword@example.com";
    db.PartnerUsers.Add(new PartnerUser { PartnerId = partner.Id, RefUser = user, IsActive = false });
    var route = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 120 };
    var trip = new ScheduledTrip { PartnerId = partner.Id, BusId = bus.Id, BusRoute = route, DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(5), AvailableSeats = 37, BaseFare = 500 };
    var foreign = new Partner { Code = "SEARCH-FOREIGN", DisplayName = "Private partner", Status = "Approved" };
    var hiddenBus = new Bus { Partner = foreign, FleetNumber = "PRIVATE-FLEET", PlateNumber = "PVT-9999", CodeDecodeBusClass = savedBus.CodeDecodeBusClass, SeatCapacity = 37 };
    var hiddenOrigin = new BusTerminal { Partner = foreign, Name = "PRIVATE-TERMINAL", LocationId = origin.LocationId };
    var hiddenDestination = new BusTerminal { Partner = foreign, Name = "Hidden destination", LocationId = destination.LocationId };
    var hiddenTrip = new ScheduledTrip { Partner = foreign, Bus = hiddenBus, BusRoute = new BusRoute { Partner = foreign, OriginTerminal = hiddenOrigin, DestinationTerminal = hiddenDestination, DurationMinutes = 60 }, DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(5), AvailableSeats = 37, BaseFare = 100 };
    var privateUser = MembershipUser("private-search-user"); privateUser.FirstName = "Private"; privateUser.LastName = "Person";
    db.PartnerUsers.Add(new PartnerUser { Partner = foreign, RefUser = privateUser });
    BusBooking Booking(ScheduledTrip schedule, string reference) => new() {
      Id = Guid.NewGuid(), RequestId = Guid.NewGuid(), ScheduledTripId = schedule.Id, Reference = reference,
      CreatedBy = PartnerApiFactory.Subject, CreatedAtUtc = DateTimeOffset.UtcNow, RequestHash = new string('A', 64), Total = 1250.50m,
      DetailsJson = JsonSerializer.Serialize(new { trip = new { origin = "Cubao", destination = "Laoag", plateNumber = "KEY-1234", departureDate = "2035-06-02", departureTime = "07:00", @operator = "Private operator" },
        booking = new { passengers = Enumerable.Range(0, 17).Select(_ => new { fullName = "Private passenger" }).ToArray(), contact = new { email = "private-contact@example.com" } } }) };
    db.ScheduledTrips.AddRange(trip, hiddenTrip); db.BusBookings.AddRange(Booking(trip, "BK-KEYWORD"), Booking(hiddenTrip, "BK-PRIVATE")); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: new Dictionary<string, object> {
      ["get-it-done-web"] = new { roles = new[] { "partner" } }, ["get-it-done-ebyahe-api"] = new { roles = new[] { "bus.read", "terminal.read", "booking.read", "member.manage" } } }));
    async Task<int> Count(string kind, string keyword, int page = 1, int size = 10)
    {
      using var response = await client.GetAsync($"/api/partner/{kind}?search={Uri.EscapeDataString(keyword)}&page={page}&pageSize={size}");
      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return json.RootElement.GetProperty("totalCount").GetInt32();
    }
    foreach (var keyword in new[] { "search-001", "key-1234", savedBus.BusClass.Decode.ToUpperInvariant(), "37", "SEARCH 37" }) Assert.Equal(1, await Count("buses", keyword));
    foreach (var keyword in new[] { "search", "Origin city", "Aurora", "Ilocos", "Adams", "search Aurora" }) Assert.Equal(1, await Count("terminals", keyword));
    foreach (var keyword in new[] { "MARIA", "santos", "maria%keyword", "Maria Santos", "santos example.com", "%" }) Assert.Equal(1, await Count("users", keyword));
    foreach (var keyword in new[] { "bk-keyword", "CUBAO", "laoag", "key-1234", "Jun 2, 2035", "7:00 AM", "17", "1250.50", "₱1,250.50", "Cubao Laoag" }) Assert.True(await Count("bookings", keyword) == 1, "Missing booking keyword: " + keyword);
    foreach (var kind in new[] { "buses", "terminals", "users", "bookings" })
    {
      foreach (var keyword in new[] { "PRIVATE", "Submitted", "Inactive", "' OR 1=1--", "[missing]" }) Assert.Equal(0, await Count(kind, keyword));
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/partner/{kind}?search={new string('x', 201)}")).StatusCode);
    }
    Assert.Equal(0, await Count("bookings", "%"));
    Assert.Equal(2, await Count("terminals", "terminal", page: 2, size: 1));
    using var paged = await client.GetAsync("/api/partner/terminals?search=terminal&pageSize=1&page=2");
    using (var json = JsonDocument.Parse(await paged.Content.ReadAsStringAsync())) Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
    Assert.Equal(0, await Count("users", "Maria missing"));
    var membership = await db.PartnerUsers.SingleAsync(row => row.RefUser.ExternalUserId == PartnerApiFactory.Subject); membership.IsActive = false; await db.SaveChangesAsync();
    foreach (var kind in new[] { "buses", "terminals", "users", "bookings" }) Assert.Equal(0, await Count(kind, ""));
  }
}
