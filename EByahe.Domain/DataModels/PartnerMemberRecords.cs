namespace EByahe.Domain.DataModels;

public sealed record PartnerMemberWorkspace(Guid Id, string Name, string BusinessCategory, IReadOnlyList<string>? AvailableRoles = null);
public sealed record PartnerMemberStatus(Guid Id, Guid PartnerId, string Email, string Status, bool HasTemporaryPassword = false);
public sealed record PartnerMemberCommand(Guid RequestId, Guid PartnerId, string FirstName, string LastName, string Email, IReadOnlyList<string>? Roles = null);
public sealed record PartnerTemporaryPassword(string Password, DateTimeOffset AvailableUntilUtc);
