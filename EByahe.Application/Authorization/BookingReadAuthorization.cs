using System.Security.Claims;

namespace EByahe.Application.Authorization;

public static class BookingReadAuthorization
{
  public const string Policy = "ReadAdminBookings";

  public static bool HasAccess(ClaimsPrincipal user) =>
    PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-web", "admin")
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "booking.read");
}
