namespace EByahe.Application.Exceptions;

public sealed class PartnerApplicationValidationException(string field, string message) : Exception(message)
{
  public Dictionary<string, string[]> Errors { get; } = new() { [field] = [message] };
}
