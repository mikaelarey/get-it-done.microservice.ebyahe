using Confluent.Kafka;
using Confluent.Kafka.Admin;
using EByahe.Application.Extensions;
using EByahe.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EByahe.Tests;

public sealed class SqlServerKafkaFactAttribute : FactAttribute
{
  public SqlServerKafkaFactAttribute()
  {
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EBYAHE_TEST_SQL_SERVER"))
      || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EBYAHE_TEST_KAFKA")))
      Skip = "Set EBYAHE_TEST_SQL_SERVER and EBYAHE_TEST_KAFKA for the real broker test.";
  }
}

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerKafkaFact]
  public async Task HostedConsumerProjectsAccountEventFromRealKafkaAndCommitsOffset()
  {
    var broker = Environment.GetEnvironmentVariable("EBYAHE_TEST_KAFKA")!;
    var topic = "ebyahe-tests-" + Guid.NewGuid().ToString("N");
    var memberTopic = topic + "-members";
    var group = topic + "-users";
    using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
    await admin.CreateTopicsAsync(new[] { topic, memberTopic }.Select(name => new TopicSpecification { Name = name, NumPartitions = 1, ReplicationFactor = 1 }));
    try
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:EByaheDatabase"] = ConnectionString,
        ["Kafka:Enabled"] = "true", ["Kafka:BootstrapServers"] = broker,
        ["Kafka:Topic"] = topic, ["Kafka:ConsumerGroup"] = group,
        ["Kafka:PartnerMembersTopic"] = memberTopic,
        ["Kafka:BrokerAddressFamily"] = "V4", ["Kafka:SecurityProtocol"] = "Plaintext", ["Kafka:RetryDelaySeconds"] = "1"
      }).Build();
      await using var provider = new ServiceCollection().AddLogging()
        .AddInfrastructure(configuration).AddApplication().BuildServiceProvider();
      var worker = provider.GetServices<IHostedService>().OfType<EByahe.Application.Events.IncomingEventWorker>().Single();
      await worker.StartAsync(CancellationToken.None);
      try
      {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
        var record = Registration();
        var delivery = await producer.ProduceAsync(topic, new Message<string, string> { Key = record.Key!, Value = record.Payload! });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (true)
        {
          await using var database = CreateDatabase();
          if (await database.IncomingLogs.AnyAsync(row => row.Status == "Acknowledged", deadline.Token)) break;
          await Task.Delay(100, deadline.Token);
        }
        await using var check = CreateDatabase();
        var receipt = await check.Incoming.SingleAsync();
        Assert.Equal(group, receipt.ConsumerGroup);
        Assert.Equal(topic, receipt.Topic);
        Assert.Equal(delivery.Partition.Value, receipt.Partition);
        Assert.Equal(delivery.Offset.Value, receipt.Offset);
        Assert.Equal(record.Payload, receipt.Payload);
        Assert.Equal("Processed", receipt.Status);
        Assert.Equal("account-user", (await check.RefUsers.SingleAsync()).ExternalUserId);
        Assert.Equal(4, await check.IncomingLogs.CountAsync());

        using var observer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
          BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4, GroupId = group, EnableAutoCommit = false
        }).Build();
        var committed = observer.Committed([new TopicPartition(topic, delivery.Partition)], TimeSpan.FromSeconds(10));
        Assert.Equal(delivery.Offset.Value + 1, committed.Single().Offset.Value);
      }
      finally
      {
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StopAsync(stopping.Token);
      }
    }
    finally { await admin.DeleteTopicsAsync([topic, memberTopic]); }
  }
}
