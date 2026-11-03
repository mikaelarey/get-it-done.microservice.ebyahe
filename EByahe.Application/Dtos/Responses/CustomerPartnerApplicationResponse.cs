namespace EByahe.Application.Dtos.Responses;

public sealed record CustomerPartnerApplicationSummary(Guid Id, string TrackingId, string BusinessName,
  string BusinessCategory, string Status, DateTimeOffset SubmittedAtUtc);

public sealed record CustomerPartnerApplicationPage(IReadOnlyList<CustomerPartnerApplicationSummary> Items,
  int TotalCount, int Page, int PageSize);

public sealed record CustomerPartnerApplicationDocument(Guid Id, string DocumentType, string FileName,
  string ContentType, long SizeBytes, DateTimeOffset UploadedAtUtc);

public sealed record CustomerPartnerDocumentContent(byte[] Content, string ContentType, string FileName);

public sealed record CustomerPartnerApplicationDetails(Guid Id, string TrackingId, string Status,
  DateTimeOffset SubmittedAtUtc, string BusinessCategory, string BusinessName, string TradeName, string EntityType,
  string Tin, string Address, string RepresentativeName, string RepresentativeEmail, string RepresentativeMobile,
  string BankName, string MaskedAccountNumber, string AccountHolder, string SettlementFrequency,
  IReadOnlyList<CustomerPartnerApplicationDocument> Documents);
