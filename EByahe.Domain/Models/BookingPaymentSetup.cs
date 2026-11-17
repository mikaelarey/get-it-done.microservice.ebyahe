namespace EByahe.Domain.Models;


public sealed class BusBookingStatusLog
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public Guid BookingId { get; set; }
  public int? CodeDecodePreviousBookingStatus { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string? PreviousStatus { get => CodeDecodePreviousBookingStatus is { } code ? CodeDecodeValues.Decode("BookingStatuses", code) : null; set => CodeDecodePreviousBookingStatus = value is null ? null : CodeDecodeValues.Code("BookingStatuses", value); }
  public int CodeDecodeBookingStatus { get; set; } = 0;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string Status { get => CodeDecodeValues.Decode("BookingStatuses", CodeDecodeBookingStatus); set => CodeDecodeBookingStatus = CodeDecodeValues.Code("BookingStatuses", value); }
  public int? CodeDecodePreviousPaymentStatus { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string? PreviousPaymentStatus { get => CodeDecodePreviousPaymentStatus is { } code ? CodeDecodeValues.Decode("PaymentStatuses", code) : null; set => CodeDecodePreviousPaymentStatus = value is null ? null : CodeDecodeValues.Code("PaymentStatuses", value); }
  public int CodeDecodePaymentStatus { get; set; } = 0;
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string PaymentStatus { get => CodeDecodeValues.Decode("PaymentStatuses", CodeDecodePaymentStatus); set => CodeDecodePaymentStatus = CodeDecodeValues.Code("PaymentStatuses", value); }
  public int? CodeDecodePaymentType { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public string? PaymentType { get => CodeDecodePaymentType is { } code ? CodeDecodeValues.Decode("PaymentTypes", code) : null; set => CodeDecodePaymentType = value is null ? null : CodeDecodeValues.Code("PaymentTypes", value); }
  public DateTimeOffset ChangedAtUtc { get; set; }
  public Guid? EventId { get; set; }
  public string Remarks { get; set; } = "";
}
