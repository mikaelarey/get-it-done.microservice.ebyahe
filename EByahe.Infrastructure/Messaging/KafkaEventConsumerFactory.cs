using EByahe.Domain.DataModels;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace EByahe.Infrastructure.Messaging;

public sealed class KafkaEventConsumerFactory(IOptions<KafkaOptions> options) : IEventConsumerFactory
{
  public IEventConsumer Create() => new KafkaEventConsumer(options.Value);

  private sealed class KafkaEventConsumer : IEventConsumer
  {
    private readonly IConsumer<string, string> consumer;

    public KafkaEventConsumer(KafkaOptions options)
    {
      consumer = new ConsumerBuilder<string, string>(options.ToConsumerConfig())
        .SetLogHandler((_, _) => { }).SetErrorHandler((_, _) => { }).Build();
      try { consumer.Subscribe(options.Topic); }
      catch { consumer.Dispose(); throw; }
    }

    public ConsumedRecord Consume(CancellationToken cancellationToken)
    {
      try
      {
        var record = consumer.Consume(cancellationToken);
        return new ConsumedRecord(record.Topic, record.Partition.Value, record.Offset.Value,
          record.Message.Key, record.Message.Value);
      }
      catch (KafkaException exception) { throw new EventTransportException("consumption", exception.Error.Code.ToString()); }
    }

    public void Commit(ConsumedRecord record)
    {
      try
      {
        consumer.Commit([new TopicPartitionOffset(record.Topic, new Partition(record.Partition), new Offset(record.Offset + 1))]);
      }
      catch (KafkaException exception) { throw new EventTransportException("acknowledgement", exception.Error.Code.ToString()); }
    }

    public void Close() => consumer.Close();
    public void Dispose() => consumer.Dispose();
  }
}
