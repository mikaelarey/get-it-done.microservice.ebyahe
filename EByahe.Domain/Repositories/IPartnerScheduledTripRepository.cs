using EByahe.Domain.DataModels;
namespace EByahe.Domain.Repositories;

public interface IPartnerScheduledTripRepository
{
  Task<PartnerRecordsPage<PartnerTravelTime>> TravelTimesAsync(string userId, int page, int pageSize, string? search, CancellationToken ct,
    Guid? originTerminalId = null, Guid? destinationTerminalId = null);
  Task<IReadOnlyList<PartnerTripFilterTerminal>> TravelTimeFilterTerminalsAsync(string userId, CancellationToken ct);
  Task<PartnerTravelTimeOptions> TravelTimeOptionsAsync(string userId, CancellationToken ct);
  Task<PartnerTravelTime?> AddTravelTimeAsync(string userId, AddPartnerTravelTime command, CancellationToken ct);
  Task<PartnerTravelTime?> UpdateTravelTimeAsync(string userId, Guid id, UpdatePartnerTravelTime command, CancellationToken ct);
  Task<PartnerTripOptions> OptionsAsync(string userId, CancellationToken cancellationToken);
  Task<PartnerPortalTrip?> AddAsync(string userId, AddPartnerScheduledTrip command, CancellationToken cancellationToken);
  Task<PartnerScheduledTripDetails?> DetailsAsync(string userId, Guid id, CancellationToken cancellationToken);
  Task<PartnerPortalTrip?> UpdateAsync(string userId, Guid id, UpdatePartnerScheduledTrip command, CancellationToken cancellationToken);
  Task<PartnerScheduledTripImportResult?> ImportAsync(string userId, Guid partnerId, IReadOnlyList<PartnerScheduledTripImportRow> rows, Guid uploadId, CancellationToken cancellationToken);
}
