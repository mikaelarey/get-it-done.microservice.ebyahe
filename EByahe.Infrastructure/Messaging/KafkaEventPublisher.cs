using System.Text;
using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace EByahe.Infrastructure.Messaging;

public sealed class KafkaEventPublisher(IOptions<KafkaOptions> options) : IEventPublisher, IDisposable
{
  private IProducer<string, string>? producer;

  public async Task<PublishedRecord> PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
  {
    try
    {
      producer ??= new ProducerBuilder<string, string>(options.Value.ToProducerConfig())
        .SetLogHandler((_, _) => { }).SetErrorHandler((_, _) => { }).Build();
      var result = await producer.ProduceAsync(message.Topic, new Message<string, string>
      {
        Key = message.MessageKey, Value = message.Payload,
        Headers = new Headers
        {
          { "eventId", Encoding.UTF8.GetBytes(message.Id.ToString()) },
          { "eventType", Encoding.UTF8.GetBytes(message.EventType) },
          { "content-type", Encoding.UTF8.GetBytes("application/json") }
        }
      }, cancellationToken);
      return new PublishedRecord(result.Partition.Value, result.Offset.Value);
    }
    catch (KafkaException exception) { throw new EventTransportException("publication", exception.Error.Code.ToString()); }
  }

  public void Dispose() => producer?.Dispose();
}
