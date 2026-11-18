namespace EByahe.Domain.DataModels;
public sealed record PartnerUserImportRow(int Row, string FirstName, string LastName, string Email, IReadOnlyList<string> Roles);
public sealed record PartnerUserImportError(int Row, string Field, string Message);
public sealed record PartnerUserImportResult(Guid UploadId, int QueuedCount, IReadOnlyList<PartnerUserImportError> Errors);
public sealed record UserUploadProgress(int RequestedCount, int PendingCount, int CompletedCount, int CancelledCount, DateTimeOffset? FinishedAtUtc);
