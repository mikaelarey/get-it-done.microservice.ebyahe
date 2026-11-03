namespace EByahe.Domain.Interfaces;

public interface IEventSettings
{
  bool Enabled { get; }
  bool ConsumerEnabled { get; }
  string Topic { get; }
  string ConsumerGroup { get; }
  int RetryDelaySeconds { get; }
}
