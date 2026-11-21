namespace EByahe.Domain.DataModels;

// Contract read by the payment service after authorizing the accepting partner member.
public sealed record PartnerCashBookingSummary(Guid Id, string Reference, string OwnerId, Guid PartnerId,
  decimal Total, string PaymentMethod, string Status, string PaymentStatus, int Passengers,
  bool CanAcceptCash, string? UnavailableReason);
