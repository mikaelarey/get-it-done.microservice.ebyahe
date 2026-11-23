namespace EByahe.Domain.Models;

public static class CashBookingPolicy
{
  public static readonly TimeSpan PaymentWindow = TimeSpan.FromHours(24);
  public const string ExpiredRemark = "Automatically cancelled: cash payment was not received within 24 hours of booking.";
  public static DateTimeOffset DueAt(BusBooking booking) => booking.CreatedAtUtc.Add(PaymentWindow);
}
