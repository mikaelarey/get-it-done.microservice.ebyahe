using System.Linq.Expressions;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerBookingRepository(EByaheDbContext db) : IPartnerBookingRepository
{
  private IQueryable<PartnerApplication> Applications(string userId) =>
    from member in db.PartnerUsers
    join application in db.PartnerApplications on member.Partner.PartnerApplicationId equals application.Id
    where member.IsActive && member.RefUser.ExternalUserId == userId && member.Partner.Status == "Approved" && application.Status == "Approved"
    select application;

  public async Task<PartnerBookingTypes> TypesAsync(string userId, CancellationToken ct)
  {
    var categories = await Applications(userId).Select(application => application.BusinessCategory).Distinct().ToArrayAsync(ct);
    return new(new[] { "bus", "hotel" }.Where(kind => categories.Contains(kind == "bus" ? "bus-operator" : "hotel")).ToArray());
  }
  private IQueryable<HotelBooking> Visible(string userId)
  {
    var applications = Applications(userId).Where(application => application.BusinessCategory == "hotel").Select(application => application.Id);
    // A hotel being removed from the catalog must not hide its reservation history.
    return db.HotelBookings.AsNoTracking().Where(booking => booking.Hotel.Partner.PartnerApplicationId.HasValue
      && applications.Contains(booking.Hotel.Partner.PartnerApplicationId.Value));
  }
  private static readonly Expression<Func<HotelBooking, PartnerHotelBooking>> Project = booking => new(booking.Id, booking.Reference,
    booking.HotelId, booking.Hotel.Name,
    booking.Hotel.Address + ", " + booking.Hotel.Barangay.Decode + ", " + booking.Hotel.Municipality.Decode + ", " + booking.Hotel.Province.Decode + ", " + booking.Hotel.Region.Decode,
    booking.GuestName, booking.GuestEmail, booking.GuestPhone, booking.RoomName, booking.RoomCount, booking.GuestCount,
    booking.CheckIn, booking.CheckOut, EF.Functions.DateDiffDay(booking.CheckIn, booking.CheckOut), booking.Total,
    booking.BookingStatus.Decode, booking.PaymentStatus.Decode, booking.CreatedAtUtc);

  public async Task<PartnerRecordsPage<PartnerHotelBooking>> HotelsAsync(string userId, int page, int pageSize, string? search, CancellationToken ct)
  {
    if (page is < 1 or > 1000000 || pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(page));
    var query = Visible(userId);
    foreach (var term in PartnerKeywordSearch.Tokens(search))
      query = query.Where(booking => EF.Functions.Collate(booking.Reference + " " + booking.Hotel.Name + " " + booking.GuestName
        + " " + booking.GuestEmail + " " + booking.RoomName + " " + booking.BookingStatus.Decode + " " + booking.PaymentStatus.Decode,
        PartnerKeywordSearch.Collation).Contains(term));
    var count = await query.CountAsync(ct);
    var items = await query.OrderByDescending(booking => booking.CreatedAtUtc).ThenBy(booking => booking.Id)
      .Skip((page - 1) * pageSize).Take(pageSize).Select(Project).ToArrayAsync(ct);
    return new(items, count, page, pageSize);
  }
  public Task<PartnerHotelBooking?> HotelDetailsAsync(string userId, Guid id, CancellationToken ct) =>
    Visible(userId).Where(booking => booking.Id == id).Select(Project).SingleOrDefaultAsync(ct);
}
