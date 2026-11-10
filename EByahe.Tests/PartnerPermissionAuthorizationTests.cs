using System.Security.Claims;
using System.Text.Json;
using EByahe.Application.Authorization;

namespace EByahe.Tests;

public sealed class PartnerPermissionAuthorizationTests
{
  [Fact]
  public void EitherMemberRoleAllowsReadingButManageDoesNotAllowCreating()
  {
    Assert.True(PartnerPortalAuthorization.CanReadMembers(User(["member.create"])));
    Assert.True(PartnerPortalAuthorization.CanReadMembers(User(["member.manage"])));
    Assert.False(PartnerPortalAuthorization.CanCreateMembers(User(["member.manage"])));
    Assert.False(PartnerPortalAuthorization.CanReadMembers(User(["booking.read"])));
    Assert.False(PartnerPortalAuthorization.CanReadMembers(User([], ["partner", "member.manage"], ["member.manage"])));
    Assert.False(PartnerPortalAuthorization.CanReadMembers(User(["member.manage"], ["admin"])));
  }

  private static ClaimsPrincipal User(string[] api, string[]? web = null, string[]? realm = null) => new(new ClaimsIdentity([
    new Claim("resource_access", JsonSerializer.Serialize(new Dictionary<string, object> {
      ["get-it-done-web"] = new { roles = web ?? ["partner"] },
      ["get-it-done-ebyahe-api"] = new { roles = api }
    })), new Claim("realm_access", JsonSerializer.Serialize(new { roles = realm ?? [] }))
  ], "test"));

  [Fact]
  public void TerminalAndTripPermissionsAreIndependentOfBusPermissionsAndRequireReadForWrite()
  {
    var bus = User(["bus.read", "bus.write"]);
    Assert.False(PartnerPortalAuthorization.CanReadTerminals(bus));
    Assert.False(PartnerPortalAuthorization.CanWriteScheduledTrips(bus));
    var terminal = User(["terminal.read", "terminal.write"]);
    Assert.True(PartnerPortalAuthorization.CanWriteTerminals(terminal));
    Assert.False(PartnerPortalAuthorization.CanReadScheduledTrips(terminal));
    Assert.True(PartnerPortalAuthorization.CanWriteScheduledTrips(User(["scheduled-trip.read", "scheduled-trip.write"])));
    Assert.False(PartnerPortalAuthorization.CanWriteTerminals(User(["terminal.write"])));
    Assert.False(PartnerPortalAuthorization.CanWriteScheduledTrips(User(["scheduled-trip.write"])));
  }

  [Fact]
  public void RealmRolesOtherClientsAndAdminDoNotGrantPartnerPermissions()
  {
    var names = new[] { "terminal.read", "terminal.write", "scheduled-trip.read", "scheduled-trip.write" };
    Assert.False(PartnerPortalAuthorization.CanReadTerminals(User([], ["partner", .. names], names)));
    Assert.False(PartnerPortalAuthorization.CanReadScheduledTrips(User([], ["partner", .. names], names)));
    Assert.False(PartnerPortalAuthorization.CanReadTerminals(User(names, ["admin"])));
    Assert.True(PartnerPortalAuthorization.CanReadTerminals(User(names)));
  }
}
