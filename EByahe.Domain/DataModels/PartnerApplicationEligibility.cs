namespace EByahe.Domain.DataModels;

public sealed record ActivePartnerApplication(Guid Id, string TrackingId, string Status);

public sealed record PartnerApplicationEligibility(bool CanSubmit, ActivePartnerApplication? ActiveApplication);
