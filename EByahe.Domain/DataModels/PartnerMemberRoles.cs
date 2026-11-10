namespace EByahe.Domain.DataModels;

public static class PartnerMemberRoles
{
  public static string[] Allowed(string category) => category switch
  {
    "bus-operator" => ["booking.read", "booking.confirm", "bus.read", "bus.write", "terminal.read", "terminal.write",
      "scheduled-trip.read", "scheduled-trip.write", "member.create", "member.manage"],
    "hotel" => ["booking.read", "booking.confirm", "hotel.read", "hotel.write", "member.create", "member.manage"],
    _ => []
  };

  // Events queued before role selection was introduced retain their previous access.
  public static string[] Legacy(string category) => Allowed(category).Where(role => role is not ("member.create" or "member.manage")).ToArray();

  public static string? RequiredRead(string role) => role == "booking.confirm" ? "booking.read"
    : role.EndsWith(".write", StringComparison.Ordinal) ? role[..^6] + ".read" : null;

  public static bool Valid(string category, IReadOnlyCollection<string>? roles) => roles is { Count: > 0 and <= 16 }
    && roles.Distinct(StringComparer.Ordinal).Count() == roles.Count
    && roles.All(role => Allowed(category).Contains(role, StringComparer.Ordinal))
    && roles.All(role => RequiredRead(role) is not {} read || roles.Contains(read, StringComparer.Ordinal));
}
