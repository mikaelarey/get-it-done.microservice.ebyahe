namespace EByahe.Domain.DataModels;
public sealed record PartnerUserAccountAccess(Guid MembershipId, Guid PartnerId, string ExternalUserId,
  string BusinessCategory, bool CanDeactivate, bool ProtectManagementPermissions);
