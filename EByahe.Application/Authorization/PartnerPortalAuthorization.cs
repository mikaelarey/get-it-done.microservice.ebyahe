using System.Security.Claims;

namespace EByahe.Application.Authorization;

public static class PartnerPortalAuthorization
{
  public const string Policy = "AccessPartnerPortal";
  public const string CreateMembersPolicy = "CreatePartnerMembers";
  public const string ReadBusesPolicy = "ReadPartnerBuses";
  public const string WriteBusesPolicy = "WritePartnerBuses";
  public static bool CanReadBuses(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "bus.read");
  public static bool CanWriteBuses(ClaimsPrincipal user) => CanReadBuses(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "bus.write");
  public static bool CanCreateMembers(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "member.create");
  public static bool HasAccess(ClaimsPrincipal user) =>
    PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-web", "partner");
}
