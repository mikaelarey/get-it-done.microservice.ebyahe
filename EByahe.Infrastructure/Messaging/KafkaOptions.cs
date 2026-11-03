using System.Text.RegularExpressions;
using Confluent.Kafka;
using EByahe.Domain.Interfaces;

namespace EByahe.Infrastructure.Messaging;

public sealed class KafkaOptions : IEventSettings
{
  public const string SectionName = "Kafka";
  public bool Enabled { get; set; }
  public bool ConsumerEnabled { get; set; } = true;
  public string BootstrapServers { get; set; } = string.Empty;
  public string Topic { get; set; } = "get-it-done.account.users.v1";
  public string ConsumerGroup { get; set; } = "get-it-done-ebyahe-account-users-v1";
  public string ClientId { get; set; } = "get-it-done-ebyahe";
  public int RetryDelaySeconds { get; set; } = 5;
  public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.Ssl;
  public string? SslCaLocation { get; set; }
  public string? SslCertificateLocation { get; set; }
  public string? SslKeyLocation { get; set; }
  public string? SslKeyPassword { get; set; }
  public string? SslKeystoreLocation { get; set; }
  public string? SslKeystorePassword { get; set; }
  public SaslMechanism SaslMechanism { get; set; } = SaslMechanism.Plain;
  public string? SaslUsername { get; set; }
  public string? SaslPassword { get; set; }

  public bool IsValid() => !string.IsNullOrWhiteSpace(Topic) && Topic.Length <= 249
    && Topic is not ("." or "..") && Regex.IsMatch(Topic, @"\A[a-zA-Z0-9._-]+\z")
    && !string.IsNullOrWhiteSpace(ConsumerGroup) && ConsumerGroup.Length <= 200
    && RetryDelaySeconds is >= 1 and <= 3600
    && (!Enabled || !ConsumerEnabled || (!string.IsNullOrWhiteSpace(BootstrapServers)
      && !string.IsNullOrWhiteSpace(ClientId) && ClientId.Length <= 200));

  public ConsumerConfig ToConsumerConfig() => new()
  {
    BootstrapServers = BootstrapServers, ClientId = ClientId, GroupId = ConsumerGroup,
    SecurityProtocol = SecurityProtocol, SslCaLocation = SslCaLocation,
    SslCertificateLocation = SslCertificateLocation, SslKeyLocation = SslKeyLocation,
    SslKeyPassword = SslKeyPassword, SslKeystoreLocation = SslKeystoreLocation,
    SslKeystorePassword = SslKeystorePassword, EnableSslCertificateVerification = true,
    SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https,
    SaslMechanism = SecurityProtocol is SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext ? SaslMechanism : null,
    SaslUsername = SaslUsername, SaslPassword = SaslPassword,
    AllowAutoCreateTopics = false, EnableAutoCommit = false, EnableAutoOffsetStore = false,
    AutoOffsetReset = AutoOffsetReset.Earliest, IsolationLevel = IsolationLevel.ReadCommitted
  };
}
