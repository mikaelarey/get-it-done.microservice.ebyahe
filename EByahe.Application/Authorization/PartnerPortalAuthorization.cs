using System.Security.Claims;

namespace EByahe.Application.Authorization;

public static class PartnerPortalAuthorization
{
  public const string Policy = "AccessPartnerPortal";
  public static bool HasAccess(ClaimsPrincipal user) =>
    PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-web", "partner");
}
