namespace EByahe.Domain.DataModels;

public sealed record UploadUserRecord(Guid Id, string Name, string Email, string Provider, DateTimeOffset CreatedAtUtc);
public sealed record UploadDetails(Guid Id, int UploadType, string UploadTypeName, string CreatedByName, DateTimeOffset DateTimeCreated,
  int TotalCount, int Page, int PageSize, IReadOnlyList<PartnerPortalBus>? Buses = null,
  IReadOnlyList<PartnerPortalTerminal>? Terminals = null, IReadOnlyList<PartnerPortalTrip>? ScheduledTrips = null,
  IReadOnlyList<UploadUserRecord>? Users = null, UserUploadProgress? UserProgress = null);
