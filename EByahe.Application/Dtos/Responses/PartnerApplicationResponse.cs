namespace EByahe.Application.Dtos.Responses;

public sealed record PartnerApplicationResponse(Guid Id, string TrackingId, string Status, DateTimeOffset SubmittedAtUtc);
