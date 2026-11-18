using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using EByahe.Application.Dtos.Responses;
using EByahe.Application.Events;
using EByahe.Application.Extensions;
using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure;
using EByahe.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private async Task<BusBookingResponse> CreateOnlineBookingAsync()
  {
    await SeedBookingTripAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    var request = BookingRequest(); request.Payment.Method = "paymongo"; request.Payment.Provider = "PayMongo";
    using var response = await client.PostAsJsonAsync("/api/bus-bookings", request);
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    return (await response.Content.ReadFromJsonAsync<BusBookingResponse>())!;
  }
  private static PaymentCompletedEvent Completion(BusBookingResponse booking) => new(Guid.NewGuid(), "payment.completed", 1,
    DateTimeOffset.UtcNow, Guid.NewGuid(), booking.Id, booking.Reference, PartnerApiFactory.Subject,
    checked((long)(booking.Total * 100m)), "PHP", "paymongo", "cs_booking_test", "pay_booking_test");
  private static ConsumedRecord PaymentRecord(PaymentCompletedEvent payment, long offset = 0) => new(
    "get-it-done.payment.completed.v1", 0, offset, payment.BookingId.ToString(),
    JsonSerializer.Serialize(payment, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
  private async Task ProcessPaymentAsync(IEventConsumer consumer, ConsumedRecord record)
  {
    await using var scope = services!.CreateAsyncScope();
    await new IncomingEventProcessor(scope.ServiceProvider.GetRequiredService<IIncomingRepository>(), new KafkaOptions(), TimeProvider.System,
      payments: scope.ServiceProvider.GetRequiredService<IBookingPaymentRepository>()).ProcessAsync(consumer, record, default);
  }

  [SqlServerFact]
  public async Task PaymentSummaryIsOwnedAndExcludesPassengerAndCardDetails()
  {
    var booking = await CreateOnlineBookingAsync();
    using var factory = new PartnerApiFactory(ConnectionString);
    using var client = factory.AuthenticatedClient();
    using var response = await client.GetAsync($"/api/bus-bookings/{booking.Id}/payment-summary");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var json = await response.Content.ReadAsStringAsync();
    Assert.Contains("paymongo", json); Assert.DoesNotContain("contact", json); Assert.DoesNotContain("fullName", json);
    using var summary = JsonDocument.Parse(json);
    Assert.Equal(1, summary.RootElement.GetProperty("passengers").GetInt32());
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(subject: "another-owner"));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/bus-bookings/{booking.Id}/payment-summary")).StatusCode);
  }
  [SqlServerFact]
  public async Task PaymentCompletionConfirmsBookingBeforeCommittingOffsetAndReplaysAreIdempotent()
  {
    var booking = await CreateOnlineBookingAsync(); var payment = Completion(booking);
    using var consumer = new TestConsumer { OnCommit = () =>
    {
      using var db = CreateDatabase(); var saved = db.BusBookings.Single();
      Assert.Equal("Confirmed", saved.Status); Assert.Equal(payment.PaymentId, saved.PaymentId);
      Assert.Equal("Processed", db.Incoming.Single().Status);
    } };
    await ProcessPaymentAsync(consumer, PaymentRecord(payment));
    await ProcessPaymentAsync(consumer, PaymentRecord(payment));
    using var replay = new TestConsumer();
    await ProcessPaymentAsync(replay, PaymentRecord(payment, 1));
    await using var db = CreateDatabase();
    var saved = await db.BusBookings.SingleAsync();
    Assert.Equal(payment.ProviderPaymentId, saved.ProviderPaymentId); Assert.Equal(1, saved.ReservedSeats);
    Assert.Equal(2, consumer.Commits); Assert.Equal(1, replay.Commits); Assert.Equal(2, await db.Incoming.CountAsync());
    Assert.Equal(39, (await db.ScheduledTrips.SingleAsync()).AvailableSeats);
    var log = Assert.Single(await db.ScheduledTripLogs.ToArrayAsync()); Assert.Equal("Updated", log.Action); Assert.Equal(PartnerApiFactory.Subject, log.CreatedBy);
    using var previousTrip = JsonDocument.Parse(log.PreviousValue!); using var currentTrip = JsonDocument.Parse(log.CurrentValue);
    Assert.Equal(40, previousTrip.RootElement.GetProperty("availableSeats").GetInt32()); Assert.Equal(39, currentTrip.RootElement.GetProperty("availableSeats").GetInt32());
  }
  [SqlServerFact]
  public async Task MismatchedAmountsAndOwnersNeverConfirmOrAcknowledgePayment()
  {
    var booking = await CreateOnlineBookingAsync(); var payment = Completion(booking);
    using var consumer = new TestConsumer();
    await Assert.ThrowsAsync<InvalidDataException>(() => ProcessPaymentAsync(consumer, PaymentRecord(payment with { AmountCentavos = 1 })));
    await Assert.ThrowsAsync<InvalidDataException>(() => ProcessPaymentAsync(consumer, PaymentRecord(payment with { OwnerId = "another-owner" }, 1)));
    await using var db = CreateDatabase(); Assert.Equal("Submitted", (await db.BusBookings.SingleAsync()).Status);
    Assert.Null((await db.BusBookings.SingleAsync()).PaymentId); Assert.Equal(0, consumer.Commits);
    Assert.All(await db.Incoming.ToListAsync(), row => Assert.Equal("Failed", row.Status));
  }

  [SqlServerKafkaFact]
  public async Task HostedConsumerConfirmsBookingFromRealPaymentKafkaTopic()
  {
    var booking = await CreateOnlineBookingAsync(); var payment = Completion(booking);
    var broker = Environment.GetEnvironmentVariable("EBYAHE_TEST_KAFKA")!;
    var prefix = "ebyahe-payment-tests-" + Guid.NewGuid().ToString("N");
    var topics = new[] { prefix + "-users", prefix + "-members", prefix + "-payments" };
    using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
    await admin.CreateTopicsAsync(topics.Select(name => new TopicSpecification { Name = name, NumPartitions = 1, ReplicationFactor = 1 }));
    try
    {
      var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:EByaheDatabase"] = ConnectionString, ["Kafka:Enabled"] = "true", ["Kafka:BootstrapServers"] = broker,
        ["Kafka:Topic"] = topics[0], ["Kafka:PartnerMembersTopic"] = topics[1], ["Kafka:PaymentCompletedTopic"] = topics[2],
        ["Kafka:ConsumerGroup"] = prefix, ["Kafka:BrokerAddressFamily"] = "V4", ["Kafka:SecurityProtocol"] = "Plaintext", ["Kafka:RetryDelaySeconds"] = "1"
      }).Build();
      await using var provider = new ServiceCollection().AddLogging().AddInfrastructure(configuration).AddApplication().BuildServiceProvider();
      var worker = provider.GetServices<IHostedService>().OfType<IncomingEventWorker>().Single();
      await worker.StartAsync(default);
      try
      {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = broker, BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
        await producer.ProduceAsync(topics[2], new Message<string, string> { Key = payment.BookingId.ToString(), Value = PaymentRecord(payment).Payload! });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (true)
        {
          await using var db = CreateDatabase();
          if (await db.IncomingLogs.AnyAsync(row => row.Status == "Acknowledged", deadline.Token)) break;
          await Task.Delay(100, deadline.Token);
        }
        await using var check = CreateDatabase();
        Assert.Equal("Confirmed", (await check.BusBookings.SingleAsync()).Status);
        Assert.Equal(payment.PaymentId, (await check.BusBookings.SingleAsync()).PaymentId);
        Assert.Equal("Processed", (await check.Incoming.SingleAsync()).Status);
      }
      finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await worker.StopAsync(stop.Token); }
    }
    finally { await admin.DeleteTopicsAsync(topics); }
  }
}
