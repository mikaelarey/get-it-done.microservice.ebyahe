using System.Security.Claims;

namespace EByahe.Application.Authorization;

public static class PartnerPortalAuthorization
{
  public const string Policy = "AccessPartnerPortal";
  public const string CreateMembersPolicy = "CreatePartnerMembers";
  public const string ReadMembersPolicy = "ReadPartnerMembers";
  public const string ManageMembersPolicy = "ManagePartnerMembers";
  public static bool CanManageMembers(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "member.manage");
  public const string ReadBusesPolicy = "ReadPartnerBuses";
  public const string ReadHotelsPolicy = "ReadPartnerHotels";
  public const string ReadRoomsPolicy = "ReadPartnerRooms";
  public const string WriteRoomsPolicy = "WritePartnerRooms";
  public static bool CanReadRooms(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "room.read");
  public static bool CanWriteRooms(ClaimsPrincipal user) => CanReadRooms(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "room.write");
  public const string WriteHotelsPolicy = "WritePartnerHotels";
  public static bool CanReadHotels(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "hotel.read");
  public static bool CanWriteHotels(ClaimsPrincipal user) => CanReadHotels(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "hotel.write");
  public const string WriteBusesPolicy = "WritePartnerBuses";
  public static bool CanReadBuses(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "bus.read");
  public static bool CanWriteBuses(ClaimsPrincipal user) => CanReadBuses(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "bus.write");
  public static bool CanCreateMembers(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "member.create");
  public static bool CanReadMembers(ClaimsPrincipal user) => CanCreateMembers(user) || (HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "member.manage"));
  public const string ReadBookingsPolicy = "ReadPartnerBookings";
  public const string ConfirmBookingsPolicy = "ConfirmPartnerBookings";
  public static bool CanConfirmBookings(ClaimsPrincipal user) => CanReadBookings(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "booking.confirm");
  public static bool CanReadBookings(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "booking.read");
  public const string ReadTerminalsPolicy = "ReadPartnerTerminals";
  public static bool CanReadTerminals(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "terminal.read");
  public const string WriteTerminalsPolicy = "WritePartnerTerminals";
  public static bool CanWriteTerminals(ClaimsPrincipal user) => CanReadTerminals(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "terminal.write");
  public const string ReadScheduledTripsPolicy = "ReadPartnerScheduledTrips";
  public static bool CanReadScheduledTrips(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "scheduled-trip.read");
  public const string WriteScheduledTripsPolicy = "WritePartnerScheduledTrips";
  public static bool CanWriteScheduledTrips(ClaimsPrincipal user) => CanReadScheduledTrips(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "scheduled-trip.write");
  public const string ReadTravelTimesPolicy = "ReadPartnerTravelTimes";
  public const string WriteTravelTimesPolicy = "WritePartnerTravelTimes";
  public static bool CanReadTravelTimes(ClaimsPrincipal user) => HasAccess(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "travel-time.read");
  public static bool CanWriteTravelTimes(ClaimsPrincipal user) => CanReadTravelTimes(user)
    && PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-ebyahe-api", "travel-time.write");
  public static bool HasAccess(ClaimsPrincipal user) =>
    PartnerApplicationReviewAuthorization.HasClientRole(user, "get-it-done-web", "partner");
}
