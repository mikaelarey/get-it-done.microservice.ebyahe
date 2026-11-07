using EByahe.Domain.Models;

namespace EByahe.Domain.DataModels;

// Internal persistence result; the API returns an explicit summary, never the stored intake snapshot.
public sealed record AdminBusBookingPage(IReadOnlyList<BusBooking> Items, int TotalCount,
  int Page, int PageSize, IReadOnlyList<string> Statuses);
