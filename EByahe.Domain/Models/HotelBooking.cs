namespace EByahe.Domain.Models;

public sealed class HotelBooking
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid HotelId { get; set; }
  public Hotel Hotel { get; set; } = null!;
  public string Reference { get; set; } = "";
  public string GuestName { get; set; } = "";
  public string GuestEmail { get; set; } = "";
  public string GuestPhone { get; set; } = "";
  // Preserve the room description as booked, independently of later catalog changes.
  public string RoomName { get; set; } = "";
  public int RoomCount { get; set; } = 1;
  public int GuestCount { get; set; } = 1;
  public DateOnly CheckIn { get; set; }
  public DateOnly CheckOut { get; set; }
  public decimal Total { get; set; }
  public int CodeDecodeBookingStatus { get; set; } = 1;
  public BookingStatus BookingStatus { get; set; } = null!;
  public int CodeDecodePaymentStatus { get; set; } = 1;
  public PaymentStatus PaymentStatus { get; set; } = null!;
  public string CreatedBy { get; set; } = "System";
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
