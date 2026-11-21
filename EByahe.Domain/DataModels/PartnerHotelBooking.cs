namespace EByahe.Domain.DataModels;

public sealed record PartnerBookingTypes(IReadOnlyList<string> Types);
public sealed record PartnerHotelBooking(Guid Id, string Reference, Guid HotelId, string HotelName, string HotelAddress,
  string GuestName, string GuestEmail, string GuestPhone, string RoomName, int RoomCount, int GuestCount,
  DateOnly CheckIn, DateOnly CheckOut, int Nights, decimal Total, string Status, string PaymentStatus, DateTimeOffset CreatedAtUtc);
