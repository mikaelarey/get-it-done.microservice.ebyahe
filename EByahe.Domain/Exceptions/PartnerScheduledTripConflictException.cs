namespace EByahe.Domain.Exceptions;
public sealed class PartnerScheduledTripConflictException(string message = "This bus already has a scheduled trip during the selected time.") : Exception(message);
