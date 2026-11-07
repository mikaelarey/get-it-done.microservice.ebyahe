using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using EByahe.Application.Events;
using EByahe.Infrastructure.Messaging;
using EByahe.Infrastructure.Persistence;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerKafkaFact]
  public async Task ApprovedPartnerOutboxPublishesItsOwnerKeyedContractThroughRealKafka()
  {
    var broker = Environment.GetEnvironmentVariable("EBYAHE_TEST_KAFKA")!;
    var topic = "ebyahe-tests-partner-" + Guid.NewGuid().ToString("N"); var notifications = topic + "-notifications";
    using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
    await admin.CreateTopicsAsync(new[] { topic, notifications }.Select(name => new TopicSpecification { Name = name, NumPartitions = 1, ReplicationFactor = 1 }));
    try
    {
      var settings = new KafkaOptions { Enabled = true, BootstrapServers = broker, SecurityProtocol = SecurityProtocol.Plaintext,
        BrokerAddressFamily = BrokerAddressFamily.V4, PartnerApprovalTopic = topic, NotificationTopic = notifications, DeliveryTimeoutMilliseconds = 3000 };
      var options = Options.Create(settings);
      await using var db = new EByaheDbContext(new DbContextOptionsBuilder<EByaheDbContext>().UseSqlServer(ConnectionString).Options, options);
      db.RefUsers.Add(MembershipUser(PartnerApiFactory.Subject));
      var application = ReviewApplication("Kafka partner", "bus-operator", "Submitted", 0, PartnerApiFactory.Subject);
      db.PartnerApplications.Add(application); await db.SaveChangesAsync();
      var repository = new PartnerApplicationReviewRepository(db, options);
      Assert.False((await repository.DecideAsync(application.Id, "UnderReview", "reviewer", null, default)).Conflict);
      var result = await repository.DecideAsync(application.Id, "Approved", "reviewer", null, default);
      Assert.False(result.Conflict);
      var declined = ReviewApplication("Kafka declined", "bus-operator", "Submitted", 0, PartnerApiFactory.Subject);
      db.PartnerApplications.Add(declined); await db.SaveChangesAsync();
      Assert.False((await repository.DecideAsync(declined.Id, "UnderReview", "reviewer", null, default)).Conflict);
      Assert.False((await repository.DecideAsync(declined.Id, "Rejected", "reviewer", "Invalid permit", default)).Conflict);
      using var publisher = new KafkaEventPublisher(options);
      var dispatcher = new OutgoingEventDispatcher(new OutboxRepository(db), publisher, settings, TimeProvider.System, NullLogger<OutgoingEventDispatcher>.Instance);
      while (await dispatcher.DispatchNextAsync(default)) { }
      db.ChangeTracker.Clear();
      var message = await db.OutboxMessages.SingleAsync(x => x.EventType == "partner.approved"); Assert.Equal("Published", message.Status);
      using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4,
        GroupId = topic + "-verify", AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false }).Build();
      consumer.Subscribe(topic);
      using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20)); var received = consumer.Consume(deadline.Token);
      Assert.Equal(PartnerApiFactory.Subject, received.Message.Key); Assert.Equal(message.Payload, received.Message.Value);
      using var json = JsonDocument.Parse(received.Message.Value);
      Assert.Equal("partner.approved", json.RootElement.GetProperty("eventType").GetString());
      Assert.Equal(application.Id, json.RootElement.GetProperty("partner").GetProperty("applicationId").GetGuid());
      Assert.Equal("bus-operator", json.RootElement.GetProperty("partner").GetProperty("businessCategory").GetString());
      Assert.Equal(3, await db.Outgoing.CountAsync(x => x.MessageId == message.Id)); consumer.Close();
      using var activityConsumer = new ConsumerBuilder<string, string>(new ConsumerConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4,
        GroupId = topic + "-status-verify", AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false }).Build();
      activityConsumer.Subscribe(notifications);
      var pending = (await db.OutboxMessages.Where(x => x.EventType == "partner-application.under-review"
        || x.EventType == "partner-application.approved" || x.EventType == "partner-application.declined").ToArrayAsync()).ToDictionary(x => x.Id);
      Assert.Equal(4, pending.Count); Assert.All(pending.Values, x => Assert.Equal("Published", x.Status));
      using var activityDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
      while (pending.Count > 0)
      {
        var activity = activityConsumer.Consume(activityDeadline.Token);
        using var payload = JsonDocument.Parse(activity.Message.Value);
        if (!pending.Remove(payload.RootElement.GetProperty("eventId").GetGuid(), out var expected)) continue;
        Assert.Equal(expected.Payload, activity.Message.Value); Assert.Equal("reviewer", activity.Message.Key);
        var recipient = Assert.Single(payload.RootElement.GetProperty("audiences").EnumerateArray());
        Assert.Equal("User", recipient.GetProperty("kind").GetString());
        Assert.Equal(PartnerApiFactory.Subject, recipient.GetProperty("externalUserId").GetString());
      }
      activityConsumer.Close();
    }
    finally { await admin.DeleteTopicsAsync([topic, notifications]); }
  }
}
