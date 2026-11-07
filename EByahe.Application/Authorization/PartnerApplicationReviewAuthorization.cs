using System.Security.Claims;
using System.Text.Json;

namespace EByahe.Application.Authorization;

public static class PartnerApplicationReviewAuthorization
{
  public const string Policy = "ReviewPartnerApplications";

  public static bool HasClientRole(ClaimsPrincipal user, string clientId, string role)
  {
    if (user.Identity?.IsAuthenticated != true) return false;
    try
    {
      using var json = JsonDocument.Parse(user.FindFirst("resource_access")?.Value ?? "null");
      return HasRole(json.RootElement, clientId, role);
    }
    catch (JsonException) { return false; }
  }

  public static bool HasAccess(ClaimsPrincipal user)
  {
    if (user.Identity?.IsAuthenticated != true) return false;
    var claim = user.FindFirst("resource_access")?.Value;
    if (claim is null) return false;
    try
    {
      using var json = JsonDocument.Parse(claim);
      return HasRole(json.RootElement, "get-it-done-web", "admin")
        && HasRole(json.RootElement, "get-it-done-ebyahe-api", "partner.review");
    }
    catch (JsonException) { return false; }
  }

  private static bool HasRole(JsonElement clients, string clientId, string role) =>
    clients.ValueKind == JsonValueKind.Object && clients.TryGetProperty(clientId, out var client)
    && client.ValueKind == JsonValueKind.Object && client.TryGetProperty("roles", out var roles)
    && roles.ValueKind == JsonValueKind.Array
    && roles.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == role);
}
