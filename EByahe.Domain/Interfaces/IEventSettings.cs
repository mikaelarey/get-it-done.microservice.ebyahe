namespace EByahe.Domain.Interfaces;

public interface IEventSettings
{
  bool Enabled { get; }
  bool ConsumerEnabled { get; }
  string Topic { get; }
  string ConsumerGroup { get; }
  string PartnerMembersTopic => "get-it-done.ebyahe.partner-members.v1";
  int RetryDelaySeconds { get; }
  int PollIntervalMilliseconds => 1000;
  int LeaseSeconds => 120;
}
