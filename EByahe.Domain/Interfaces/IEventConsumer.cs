using EByahe.Domain.DataModels;

namespace EByahe.Domain.Interfaces;

public interface IEventConsumer : IDisposable
{
  ConsumedRecord Consume(CancellationToken cancellationToken);
  void Commit(ConsumedRecord record);
  void Close();
}

public interface IEventConsumerFactory
{
  IEventConsumer Create();
}
