using EByahe.Domain.Models;
using EByahe.Domain.DataModels;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class UploadRepository(EByaheDbContext db) : IUploadRepository
{
  public async Task<UploadDetails?> DetailsAsync(string userId, Guid id, IReadOnlyCollection<int> allowedTypes, int page, int pageSize, CancellationToken ct)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var partners = db.PartnerUsers.Where(member => member.IsActive && member.RefUser.ExternalUserId == userId && member.Partner.Status == "Approved")
      .Select(member => member.PartnerId).Distinct();
    if (!await partners.AnyAsync(ct)) return null;
    var metadata = await (from upload in db.Uploads.AsNoTracking()
      join actor in db.RefUsers on upload.CreatedBy equals actor.ExternalUserId
      join type in db.CodeDecodeUploadTypes on upload.UploadType equals type.Code
      where upload.Id == id && allowedTypes.Contains(upload.UploadType)
      select new { upload.Id, upload.UploadType, upload.CreatedBy, upload.DateTimeCreated, TypeName = type.Decode,
        ActorName = (actor.FirstName + " " + actor.LastName).Trim() }).SingleOrDefaultAsync(ct);
    if (metadata is null) return null;
    var links = db.UploadRecords.AsNoTracking().Where(link => link.UploadId == id);
    var count = await links.CountAsync(ct);
    if (count == 0 && metadata.CreatedBy != userId) return null;
    var details = new UploadDetails(id, metadata.UploadType, metadata.TypeName, metadata.ActorName, metadata.DateTimeCreated, count, page, pageSize);
    var skip = (page - 1) * pageSize;
    // A batch is visible only when every linked record is in the caller's current workspaces.
    // Do not disclose a partial batch, its count, or metadata from another partner.
    switch (metadata.UploadType)
    {
      case UploadTypeCodes.Buses:
        var buses = db.Buses.AsNoTracking().Where(bus => partners.Contains(bus.PartnerId) && links.Any(link => link.BusId == bus.Id));
        if (await buses.CountAsync(ct) != count) return null;
        return details with { Buses = await buses.OrderBy(bus => bus.FleetNumber).ThenBy(bus => bus.Id).Skip(skip).Take(pageSize)
          .Select(bus => new PartnerPortalBus(bus.Id, bus.PartnerId, bus.Partner.DisplayName, bus.FleetNumber, bus.PlateNumber,
            bus.BusClass.Decode, bus.SeatCapacity, bus.IsActive)).ToArrayAsync(ct) };
      case UploadTypeCodes.Terminals:
        var terminals = db.BusTerminals.AsNoTracking().Where(terminal => partners.Contains(terminal.PartnerId) && links.Any(link => link.TerminalId == terminal.Id));
        if (await terminals.CountAsync(ct) != count) return null;
        return details with { Terminals = await terminals.OrderBy(terminal => terminal.Name).ThenBy(terminal => terminal.Id).Skip(skip).Take(pageSize)
          .Select(terminal => new PartnerPortalTerminal(terminal.Id, terminal.Name, terminal.Location.Name,
            TerminalAddress.Format(terminal.Address, terminal.Location.Barangay == null ? null : terminal.Location.Barangay.Decode,
              terminal.Location.Municipality == null ? null : terminal.Location.Municipality.Decode,
              terminal.Location.Province == null ? null : terminal.Location.Province.Decode,
              terminal.Location.Region == null ? null : terminal.Location.Region.Decode), terminal.Partner.DisplayName, terminal.IsActive)).ToArrayAsync(ct) };
      case UploadTypeCodes.ScheduledTrips:
        var trips = db.ScheduledTrips.AsNoTracking().Where(trip => partners.Contains(trip.PartnerId) && links.Any(link => link.ScheduledTripId == trip.Id));
        if (await trips.CountAsync(ct) != count) return null;
        return details with { ScheduledTrips = await trips.OrderBy(trip => trip.DepartureAtUtc).ThenBy(trip => trip.Id).Skip(skip).Take(pageSize)
          .Select(trip => new PartnerPortalTrip(trip.Id, trip.BusRoute.OriginTerminal.Location.Name, trip.BusRoute.DestinationTerminal.Location.Name,
            trip.Partner.DisplayName, trip.Bus.PlateNumber, trip.Bus.BusClass.Decode, trip.DepartureAtUtc, trip.AvailableSeats, trip.BaseFare, trip.Status)).ToArrayAsync(ct) };
      case UploadTypeCodes.Users:
        var batch = await db.UserUploadBatches.AsNoTracking().SingleOrDefaultAsync(x => x.UploadId == id, ct);
        if (batch is not null) {
          if (!await partners.ContainsAsync(batch.PartnerId, ct)) return null;
          var requests = db.UploadMemberRequests.Where(link => link.UploadId == id);
          details = details with { UserProgress = new(batch.RequestedCount,
            await requests.CountAsync(link => link.Request.CodeDecodePartnerMemberRequestStatus == 1, ct),
            await requests.CountAsync(link => link.Request.CodeDecodePartnerMemberRequestStatus == 2, ct),
            await requests.CountAsync(link => link.Request.CodeDecodePartnerMemberRequestStatus == 3, ct), batch.FinishedAtUtc) };
        }
        var users = db.RefUsers.AsNoTracking().Where(user => links.Any(link => link.RefUserId == user.Id)
          && user.PartnerMemberships.Any(member => partners.Contains(member.PartnerId)));
        if (await users.CountAsync(ct) != count) return null;
        return details with { Users = await users.OrderBy(user => user.FirstName).ThenBy(user => user.LastName).ThenBy(user => user.Id).Skip(skip).Take(pageSize)
          .Select(user => new UploadUserRecord(user.Id, (user.FirstName + " " + user.LastName).Trim(), user.Email, user.Provider, user.CreatedAtUtc)).ToArrayAsync(ct) };
      default: return null;
    }
  }

  public async Task<Guid> AddAsync(string userId, int uploadType, CancellationToken cancellationToken, Guid? requestedId = null)
  {
    if (requestedId.HasValue) {
      var previous = await db.Uploads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestedId.Value, cancellationToken);
      if (previous is not null) {
        if (previous.CreatedBy != userId || previous.UploadType != uploadType) throw new EByahe.Domain.Exceptions.PartnerMemberRequestConflictException();
        return previous.Id;
      }
    }
    var upload = new Upload { Id = requestedId ?? Guid.NewGuid(), CreatedBy = userId, UploadType = uploadType };
    db.Uploads.Add(upload);
    // Persist the attempt before parsing/importing so rejected files also retain an entry.
    try { await db.SaveChangesAsync(cancellationToken); }
    catch (DbUpdateException error) when (requestedId.HasValue && error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) {
      db.Entry(upload).State = EntityState.Detached;
      var previous = await db.Uploads.AsNoTracking().SingleAsync(x => x.Id == requestedId.Value, cancellationToken);
      if (previous.CreatedBy != userId || previous.UploadType != uploadType) throw new EByahe.Domain.Exceptions.PartnerMemberRequestConflictException();
    }
    return upload.Id;
  }
}
