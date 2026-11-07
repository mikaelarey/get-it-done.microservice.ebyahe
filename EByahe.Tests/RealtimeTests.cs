using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EByahe.Domain.Repositories;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task SignalRRecordChangesReachCurrentPartnerMembersAndAuthorizedReviewersOnlyAfterCommit()
  {
    var partner = await SeedMemberPartner();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var http = factory.AuthenticatedClient();
    http.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsync("/api/live/negotiate?negotiateVersion=1", null)).StatusCode);
    Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/partner/buses?access_token=" + factory.Token())).StatusCode);
    using var member = await HubTestClient.ConnectAsync(factory.Server, "/api/live", factory.Token());
    using var outsider = await HubTestClient.ConnectAsync(factory.Server, "/api/live", factory.Token("outsider"));
    var reviewerRoles = new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "admin" } },
      ["get-it-done-ebyahe-api"] = new { roles = new[] { "partner.review" } } };
    using var reviewer = await HubTestClient.ConnectAsync(factory.Server, "/api/live", factory.Token("reviewer", resourceAccess: reviewerRoles));
    http.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var response = await http.PostAsJsonAsync("/api/partner/buses", new { partnerId = partner.Id, fleetNumber = "LIVE-001", plateNumber = "LIVE-123", busClass = "Deluxe", seatCapacity = 40 });
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    await HubTestClient.ExpectAsync(member, "RecordsChanged"); await HubTestClient.ExpectAsync(reviewer, "RecordsChanged");
    await using var db = CreateDatabase(); Assert.True(await db.Buses.AnyAsync(x => x.PlateNumber == "LIVE-123"));
    // No event for rejected writes.
    Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync("/api/partner/buses", new { partnerId = partner.Id, fleetNumber = "LIVE-002", plateNumber = "LIVE-123", busClass = "Deluxe", seatCapacity = 40 })).StatusCode);
    using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HubTestClient.ReceiveAsync(outsider, timeout.Token));
  }
}
