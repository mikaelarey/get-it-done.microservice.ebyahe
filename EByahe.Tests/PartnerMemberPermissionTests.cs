using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task MemberCreatorCanAssignAllBusPermissionsWithOnlyMemberCreateAndRetryPreservesSelection()
  {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("member.create")));
    var workspace = Assert.Single((await client.GetFromJsonAsync<PartnerMemberWorkspace[]>("/api/partner/users/workspaces"))!);
    string[] expected = ["booking.confirm", "booking.read", "bus.read", "bus.write", "member.create", "member.manage",
      "scheduled-trip.read", "scheduled-trip.write", "terminal.read", "terminal.write", "travel-time.read", "travel-time.write"];
    Assert.Equal(expected, workspace.AvailableRoles!.Order());
    var command = new { requestId = Guid.NewGuid(), partnerId = partner.Id, firstName = "Permission", lastName = "Member", email = "permissions@example.com", roles = expected };
    foreach (var invalid in new[] { Array.Empty<string>(), new[] { "hotel.read" }, new[] { "hotel.write" }, new[] { "partner.read" }, new[] { "partner.review" }, new[] { "admin" }, new[] { "terminal.write" }, new[] { "terminal.read", "terminal.read" } })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/users", command with { roles = invalid })).StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/partner/users", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/partner/users", command with { roles = command.roles.Reverse().ToArray() })).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/users", command with { roles = new[] { "terminal.read" } })).StatusCode);
    await using var db = CreateDatabase();
    var request = await db.PartnerMemberRequests.SingleAsync();
    Assert.Equal(command.roles, JsonSerializer.Deserialize<string[]>(request.RequestedRolesJson!));
    using var payload = JsonDocument.Parse((await db.OutboxMessages.SingleAsync(x => x.EventType == "partner.member.requested")).Payload);
    Assert.Equal(2, payload.RootElement.GetProperty("schemaVersion").GetInt32());
    Assert.Equal(command.roles, payload.RootElement.GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToArray());
    Assert.Equal(1, await db.PartnerMemberRequests.CountAsync());
  }

  [SqlServerFact]
  public async Task HotelPartnerStillOffersOnlyHotelBookingAndMemberPermissions()
  {
    await SeedMemberPartner("hotel"); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("member.create")));
    var workspace = Assert.Single((await client.GetFromJsonAsync<PartnerMemberWorkspace[]>("/api/partner/users/workspaces"))!);
    Assert.Equal(new[] { "booking.confirm", "booking.read", "hotel.read", "hotel.write", "member.create", "member.manage" }, workspace.AvailableRoles!.Order());
  }
}
