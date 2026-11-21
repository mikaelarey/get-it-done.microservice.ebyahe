using System.Data;
using System.Linq.Expressions;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerHotelRepository(EByaheDbContext db) : IPartnerHotelRepository
{
  private IQueryable<Partner> Accessible(string userId) => db.Partners.Where(partner => partner.Status == "Approved"
    && db.PartnerUsers.Any(member => member.PartnerId == partner.Id && member.IsActive && member.RefUser.ExternalUserId == userId)
    && db.PartnerApplications.Any(application => application.Id == partner.PartnerApplicationId
      && application.Status == "Approved" && application.BusinessCategory == "hotel"));

  private static readonly Expression<Func<Hotel, PartnerHotel>> Project = hotel => new(hotel.Id, hotel.PartnerId,
    hotel.Partner.DisplayName, hotel.Name, hotel.Address, hotel.CodeDecodeRegion, hotel.Region.Decode,
    hotel.CodeDecodeProvince, hotel.Province.Decode, hotel.CodeDecodeMunicipality, hotel.Municipality.Decode,
    hotel.CodeDecodeBarangay, hotel.Barangay.Decode, hotel.ContactNumber, hotel.Email, hotel.Description, hotel.IsActive);

  public async Task<PartnerRecordsPage<PartnerHotel>> ListAsync(string userId, int page, int pageSize, string? search, CancellationToken ct)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var ids = Accessible(userId).Select(partner => partner.Id);
    var query = db.Hotels.AsNoTracking().Where(hotel => !hotel.IsDeleted && ids.Contains(hotel.PartnerId));
    foreach (var term in PartnerKeywordSearch.Tokens(search))
      query = query.Where(hotel => EF.Functions.Collate(hotel.Name + " " + hotel.Partner.DisplayName + " " + hotel.Address
        + " " + hotel.Region.Decode + " " + hotel.Province.Decode + " " + hotel.Municipality.Decode + " " + hotel.Barangay.Decode
        + " " + hotel.ContactNumber + " " + hotel.Email, PartnerKeywordSearch.Collation).Contains(term));
    var count = await query.CountAsync(ct);
    var items = await query.OrderByDescending(hotel => hotel.CreatedAtUtc).ThenBy(hotel => hotel.Id)
      .Skip((page - 1) * pageSize).Take(pageSize).Select(Project).ToArrayAsync(ct);
    return new(items, count, page, pageSize);
  }

  public async Task<IReadOnlyList<PartnerHotelWorkspace>> WorkspacesAsync(string userId, CancellationToken ct) =>
    await Accessible(userId).AsNoTracking().OrderBy(partner => partner.DisplayName).ThenBy(partner => partner.Id)
      .Select(partner => new PartnerHotelWorkspace(partner.Id, partner.DisplayName)).ToArrayAsync(ct);

  public async Task<PartnerHotel?> AddAsync(string userId, AddPartnerHotel command, CancellationToken ct)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    if (!await Accessible(userId).AnyAsync(partner => partner.Id == command.PartnerId, ct)) return null;
    await ValidateAddress(command, ct);
    var hotel = new Hotel { PartnerId = command.PartnerId, Name = command.Name, Address = command.Address,
      CodeDecodeRegion = command.CodeDecodeRegion, CodeDecodeProvince = command.CodeDecodeProvince,
      CodeDecodeMunicipality = command.CodeDecodeMunicipality, CodeDecodeBarangay = command.CodeDecodeBarangay,
      ContactNumber = command.ContactNumber, Email = command.Email, Description = command.Description, CreatedBy = userId };
    db.Hotels.Add(hotel);
    CatalogAudit.Record(db, hotel, userId, "Created");
    try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerHotelConflictException(); }
    return await db.Hotels.AsNoTracking().Where(row => row.Id == hotel.Id).Select(Project).SingleAsync(ct);
  }

  public Task<PartnerHotel?> GetAsync(string userId, Guid id, CancellationToken ct)
  {
    var ids = Accessible(userId).Select(partner => partner.Id);
    return db.Hotels.AsNoTracking().Where(hotel => hotel.Id == id && !hotel.IsDeleted && ids.Contains(hotel.PartnerId)).Select(Project).SingleOrDefaultAsync(ct);
  }

  public async Task<PartnerHotel?> UpdateAsync(string userId, Guid id, AddPartnerHotel command, bool isActive, CancellationToken ct)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var ids = Accessible(userId).Select(partner => partner.Id);
    // A hotel remains with the company that created it, even if the caller belongs to several companies.
    var hotel = await ForMutation(id).SingleOrDefaultAsync(row => !row.IsDeleted && row.PartnerId == command.PartnerId && ids.Contains(row.PartnerId), ct);
    if (hotel is null) return null;
    await ValidateAddress(command, ct);
    var previous = CatalogAudit.Snapshot(hotel);
    hotel.Name = command.Name; hotel.Address = command.Address; hotel.ContactNumber = command.ContactNumber;
    hotel.Email = command.Email; hotel.Description = command.Description; hotel.IsActive = isActive;
    hotel.CodeDecodeRegion = command.CodeDecodeRegion; hotel.CodeDecodeProvince = command.CodeDecodeProvince;
    hotel.CodeDecodeMunicipality = command.CodeDecodeMunicipality; hotel.CodeDecodeBarangay = command.CodeDecodeBarangay;
    CatalogAudit.Record(db, hotel, userId, "Updated", previous);
    try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
    catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
    { throw new PartnerHotelConflictException(); }
    return await db.Hotels.AsNoTracking().Where(row => row.Id == id).Select(Project).SingleAsync(ct);
  }

  // Serialize mutations for the same hotel so concurrent deletes produce one audit entry.
  private IQueryable<Hotel> ForMutation(Guid id) => db.Hotels.FromSqlInterpolated($"SELECT * FROM [Hotels] WITH (UPDLOCK) WHERE [Id] = {id}");

  public async Task<bool> DeleteAsync(string userId, Guid id, CancellationToken ct)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var ids = Accessible(userId).Select(partner => partner.Id);
    var hotel = await ForMutation(id).SingleOrDefaultAsync(row => ids.Contains(row.PartnerId), ct);
    if (hotel is null) return false;
    if (hotel.IsDeleted) { await transaction.CommitAsync(ct); return true; }
    var previous = CatalogAudit.Snapshot(hotel);
    hotel.IsDeleted = true; hotel.DeletedBy = userId; hotel.DeletedAtUtc = DateTimeOffset.UtcNow;
    CatalogAudit.Record(db, hotel, userId, "Deleted", previous);
    await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    return true;
  }

  private async Task ValidateAddress(AddPartnerHotel command, CancellationToken ct)
  {
    if (!await db.CodeDecodeBarangays.AnyAsync(barangay => barangay.Code == command.CodeDecodeBarangay
      && barangay.CodeDecodeMunicipality == command.CodeDecodeMunicipality
      && barangay.Municipality.CodeDecodeProvince == command.CodeDecodeProvince
      && barangay.Municipality.Province.CodeDecodeRegion == command.CodeDecodeRegion, ct)) throw new PartnerHotelAddressException();
  }
}
