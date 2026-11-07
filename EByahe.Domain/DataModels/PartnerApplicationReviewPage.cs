namespace EByahe.Domain.DataModels;

public sealed record PartnerApplicationReviewItem(Guid Id, string TrackingId, string BusinessName,
  string BusinessCategory, string Address, string RepresentativeName, string RepresentativeEmail,
  DateTimeOffset SubmittedAtUtc, string Status);

public sealed record PartnerApplicationReviewSummary(int TotalApplications, int AwaitingReview, int Approved);

public sealed record PartnerApplicationReviewPage(IReadOnlyList<PartnerApplicationReviewItem> Items,
  int TotalCount, int Page, int PageSize, PartnerApplicationReviewSummary Summary,
  IReadOnlyList<string> Categories, IReadOnlyList<string> Statuses);
