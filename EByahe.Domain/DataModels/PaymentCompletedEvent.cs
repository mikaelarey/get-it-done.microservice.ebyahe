namespace EByahe.Domain.DataModels;

// Contract: get-it-done.payment.completed.v1, keyed by BookingId.
public sealed record PaymentCompletedEvent(Guid EventId, string EventType, int SchemaVersion, DateTimeOffset OccurredAtUtc,
  Guid PaymentId, Guid BookingId, string BookingReference, string OwnerId, long AmountCentavos, string Currency,
  string Provider, string ProviderSessionId, string ProviderPaymentId, string? PaymentType = null, string PaymentStatus = "Payment Success");
