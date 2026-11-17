namespace EByahe.Domain.Models;

public sealed class BusBooking
{
  public Guid Id { get; set; }
  public Guid RequestId { get; set; }
  public Guid? ScheduledTripId { get; set; }
  public int ReservedSeats { get; set; }
  public int CodeDecodePaymentStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string PaymentStatus { get => CodeDecodeValues.Decode("PaymentStatuses", CodeDecodePaymentStatus); set => CodeDecodePaymentStatus = CodeDecodeValues.Code("PaymentStatuses", value); }
  public int? CodeDecodePaymentType { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string? PaymentType { get => CodeDecodePaymentType is { } code ? CodeDecodeValues.Decode("PaymentTypes", code) : null; set => CodeDecodePaymentType = value is null ? null : CodeDecodeValues.Code("PaymentTypes", value); }
  public bool PaymentReviewRequired { get; set; }
  public string CreatedBy { get; set; } = string.Empty;
  public string Reference { get; set; } = string.Empty;
  public DateTimeOffset CreatedAtUtc { get; set; }
  public int CodeDecodeBookingStatus { get; set; } = 1;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("BookingStatuses", CodeDecodeBookingStatus); set => CodeDecodeBookingStatus = CodeDecodeValues.Code("BookingStatuses", value); }
  public Guid? PaymentId { get; set; }
  public string? ProviderPaymentId { get; set; }
  public DateTimeOffset? PaidAtUtc { get; set; }
  public decimal Total { get; set; }
  public string RequestHash { get; set; } = string.Empty;
  // Validated intake snapshot. Contains safe payment metadata, never card numbers or CVVs.
  public string DetailsJson { get; set; } = string.Empty;
}
