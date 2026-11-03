namespace EByahe.Application.Events;

public sealed record UserRegisteredEvent(Guid EventId, string EventType, int SchemaVersion,
  DateTimeOffset OccurredAtUtc, RegisteredUser User);

public sealed record RegisteredUser(string Id, string FirstName, string LastName, string Email,
  string Provider, string Role);
