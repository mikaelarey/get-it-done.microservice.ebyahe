namespace EByahe.Domain.DataModels;

public sealed record PartnerDirectoryItem(Guid Id, string Code, string BusinessName,
  string? BusinessCategory, string? Address, string? ContactName, string? ContactEmail,
  DateTimeOffset? JoinedAtUtc, string Status);

public sealed record PartnerDirectorySummary(int TotalPartners, int Approved, int Suspended);

public sealed record PartnerDirectoryPage(IReadOnlyList<PartnerDirectoryItem> Items,
  int TotalCount, int Page, int PageSize, PartnerDirectorySummary Summary,
  IReadOnlyList<string> Categories, IReadOnlyList<string> Statuses);

public sealed record PartnerOwnerDetails(string FullName, string Email);

public sealed record PartnerDirectoryDetails(PartnerDirectoryItem Partner, Guid? ApplicationId,
  string? ApplicationTrackingId, PartnerOwnerDetails? Owner,
  int BusCount, int TerminalCount, int ActiveUserCount, int BookingCount);
