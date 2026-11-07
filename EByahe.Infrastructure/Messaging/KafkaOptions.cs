using System.Text.RegularExpressions;
using EByahe.Domain.Interfaces;
using Confluent.Kafka;

namespace EByahe.Infrastructure.Messaging;

public sealed class KafkaOptions : IEventSettings
{
  public const string SectionName = "Kafka";
  public bool Enabled { get; set; }
  public string NotificationTopic { get; set; } = ActivityOutbox.DefaultTopic;
  public string PartnerApprovalTopic { get; set; } = PartnerApprovalOutbox.DefaultTopic;
  public bool ConsumerEnabled { get; set; } = true;
  public string BootstrapServers { get; set; } = string.Empty;
  public string Topic { get; set; } = "get-it-done.account.users.v1";
  public string ConsumerGroup { get; set; } = "get-it-done-ebyahe-account-users-v1";
  public string ClientId { get; set; } = "get-it-done-ebyahe";
  public int PollIntervalMilliseconds { get; set; } = 1000;
  public int RetryDelaySeconds { get; set; } = 5;
  public int LeaseSeconds { get; set; } = 120;
  public int DeliveryTimeoutMilliseconds { get; set; } = 30000;
  public BrokerAddressFamily BrokerAddressFamily { get; set; } = BrokerAddressFamily.Any;
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

  public bool IsValid() => !string.IsNullOrWhiteSpace(NotificationTopic) && NotificationTopic.Length <= 249
    && !string.IsNullOrWhiteSpace(PartnerApprovalTopic) && PartnerApprovalTopic.Length <= 249
    && PartnerApprovalTopic is not ("." or "..") && Regex.IsMatch(PartnerApprovalTopic, @"\A[a-zA-Z0-9._-]+\z")
    && PartnerApprovalTopic != NotificationTopic && PartnerApprovalTopic != Topic
    && NotificationTopic is not ("." or "..") && Regex.IsMatch(NotificationTopic, @"\A[a-zA-Z0-9._-]+\z")
    && Topic.Length is > 0 and <= 249 && Topic is not ("." or "..")
    && Regex.IsMatch(Topic, @"\A[a-zA-Z0-9._-]+\z")
    && (!Enabled || (!string.IsNullOrWhiteSpace(BootstrapServers)
      && !string.IsNullOrWhiteSpace(ClientId) && ClientId.Length <= 200
      && !string.IsNullOrWhiteSpace(ConsumerGroup) && ConsumerGroup.Length <= 200
      && PollIntervalMilliseconds is >= 100 and <= 60000 && RetryDelaySeconds is >= 1 and <= 3600
      && DeliveryTimeoutMilliseconds is >= 1000 and <= 300000
      && LeaseSeconds * 1000L > DeliveryTimeoutMilliseconds + 30000L));

  private ClientConfig ClientConfiguration() => new()
  {
    BootstrapServers = BootstrapServers, ClientId = ClientId, BrokerAddressFamily = BrokerAddressFamily,
    SecurityProtocol = SecurityProtocol, SslCaLocation = SslCaLocation,
    SslCertificateLocation = SslCertificateLocation, SslKeyLocation = SslKeyLocation,
    SslKeyPassword = SslKeyPassword, SslKeystoreLocation = SslKeystoreLocation,
    SslKeystorePassword = SslKeystorePassword, EnableSslCertificateVerification = true,
    SslEndpointIdentificationAlgorithm = SslEndpointIdentificationAlgorithm.Https,
    SaslMechanism = SecurityProtocol is SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext ? SaslMechanism : null,
    SaslUsername = SaslUsername, SaslPassword = SaslPassword,
    AllowAutoCreateTopics = false
  };

  public ProducerConfig ToProducerConfig() => new(ClientConfiguration())
  {
    EnableIdempotence = true, Acks = Acks.All, MessageTimeoutMs = DeliveryTimeoutMilliseconds
  };

  public ConsumerConfig ToConsumerConfig() => new(ClientConfiguration())
  {
    GroupId = ConsumerGroup, EnableAutoCommit = false, EnableAutoOffsetStore = false,
    AutoOffsetReset = AutoOffsetReset.Earliest, IsolationLevel = IsolationLevel.ReadCommitted
  };
}
