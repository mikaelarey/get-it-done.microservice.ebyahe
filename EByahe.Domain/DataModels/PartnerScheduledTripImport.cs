namespace EByahe.Domain.DataModels;

public sealed record PartnerScheduledTripImportRow(string Sheet, int Row, AddPartnerScheduledTrip Trip);
public sealed record PartnerScheduledTripImportError(string Sheet, int Row, string Field, string Message);
public sealed record PartnerScheduledTripImportResult(int ImportedCount, IReadOnlyList<PartnerScheduledTripImportError> Errors, Guid? UploadId = null);
