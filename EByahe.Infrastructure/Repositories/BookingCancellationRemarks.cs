using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Infrastructure.Repositories;

internal static class BookingCancellationRemarks
{
  public static async Task<BusBooking?> LoadAsync(EByaheDbContext db, BusBooking? booking, CancellationToken ct)
  {
    if (booking?.Status == "Cancelled")
      booking.CancellationRemarks = await db.BusBookingStatusLogs.AsNoTracking().Where(log => log.BookingId == booking.Id && log.Status == "Cancelled")
        .OrderByDescending(log => log.ChangedAtUtc).ThenByDescending(log => log.Id).Select(log => log.Remarks).FirstOrDefaultAsync(ct);
    return booking;
  }
}
