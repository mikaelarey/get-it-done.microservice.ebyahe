using Confluent.Kafka;
using EByahe.Infrastructure.Messaging;

namespace EByahe.Tests;

public sealed class KafkaConfigurationTests
{
  [Fact]
  public void ConsumerRequiresManualAcknowledgementAndUsesIndependentGroup()
  {
    var options = new KafkaOptions();
    var config = options.ToConsumerConfig();
    Assert.Equal(false, config.EnableAutoCommit);
    Assert.Equal(false, config.EnableAutoOffsetStore);
    Assert.Equal(false, config.AllowAutoCreateTopics);
    Assert.Equal(AutoOffsetReset.Earliest, config.AutoOffsetReset);
    Assert.Equal(IsolationLevel.ReadCommitted, config.IsolationLevel);
    Assert.Equal("get-it-done.account.users.v1", options.Topic);
    Assert.Equal("get-it-done-ebyahe-account-users-v1", config.GroupId);
  }

  [Theory]
  [InlineData("", false)]
  [InlineData("localhost:9092", true)]
  public void EnabledConsumerRequiresBroker(string broker, bool expected)
  {
    Assert.Equal(expected, new KafkaOptions { Enabled = true, BootstrapServers = broker }.IsValid());
  }
}
