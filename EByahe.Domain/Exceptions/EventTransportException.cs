namespace EByahe.Domain.Exceptions;

// Only a safe error code is exposed; no broker payloads or credentials.
public sealed class EventTransportException(string operation, string code)
  : Exception($"Kafka {operation} failed ({code}).");
