using EByahe.Application.Events;
using EByahe.Application.Extensions;
using EByahe.Domain.Models;
using EByahe.Infrastructure;
using EByahe.Infrastructure.Messaging;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerKafkaFact]
  public async Task PartnerMemberConsumerCompletesMembershipThroughRealKafkaAndReplaysSafely()
  {
    var broker = Environment.GetEnvironmentVariable("EBYAHE_TEST_KAFKA")!;
    var partner = await SeedMemberPartner(); var prefix = "ebyahe-tests-members-" + Guid.NewGuid().ToString("N");
    var topics = new[] { prefix + "-users", prefix + "-members" };
    using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
    await admin.CreateTopicsAsync(topics.Select(name => new TopicSpecification { Name = name, NumPartitions = 1, ReplicationFactor = 1 }));
    try
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:EByaheDatabase"] = ConnectionString, ["Kafka:Enabled"] = "true", ["Kafka:BootstrapServers"] = broker,
        ["Kafka:Topic"] = topics[0], ["Kafka:PartnerMembersTopic"] = topics[1], ["Kafka:ConsumerGroup"] = prefix,
        ["Kafka:SecurityProtocol"] = "Plaintext", ["Kafka:BrokerAddressFamily"] = "V4", ["Kafka:RetryDelaySeconds"] = "1",
        ["PartnerMembers:PasswordEncryptionKey"] = Convert.ToBase64String(new byte[32]) }).Build();
      await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).AddApplication().BuildServiceProvider();
      await using var scope = services.CreateAsyncScope();
      var repository = scope.ServiceProvider.GetRequiredService<EByahe.Domain.Repositories.IPartnerMemberRepository>();
      var requestId = Guid.NewGuid(); await repository.QueueAsync(PartnerApiFactory.Subject,
        new(requestId, partner.Id, "New", "Member", "new@example.com"), default);
      await using var db = CreateDatabase(); var request = await db.PartnerMemberRequests.SingleAsync();
      var outgoing = await db.OutboxMessages.SingleAsync(x => x.EventType == "partner.member.requested");
      var worker = services.GetServices<IHostedService>().OfType<IncomingEventWorker>().Single(); await worker.StartAsync(default);
      try
      {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
        await producer.ProduceAsync(topics[1], new Message<string, string> { Key = requestId.ToString(), Value = outgoing.Payload });
        var reply = MemberReply(request, true);
        await producer.ProduceAsync(topics[1], new Message<string, string> { Key = reply.Key!, Value = reply.Payload! });
        await producer.ProduceAsync(topics[1], new Message<string, string> { Key = reply.Key!, Value = reply.Payload! });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        while (await db.IncomingLogs.CountAsync(x => x.Status == "Acknowledged", timeout.Token) < 3) await Task.Delay(100, timeout.Token);
        Assert.Equal(2, await db.PartnerUsers.CountAsync()); Assert.Equal(2, await db.RefUsers.CountAsync());
        Assert.Equal("Completed", (await db.PartnerMemberRequests.AsNoTracking().SingleAsync()).Status);
        Assert.True((await db.TemporaryPasswords.AsNoTracking().SingleAsync()).CanReveal);
      }
      finally { using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await worker.StopAsync(stopping.Token); }
    }
    finally { await admin.DeleteTopicsAsync(topics); }
  }
}
