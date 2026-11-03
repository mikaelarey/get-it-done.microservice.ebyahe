using System.Net.Mail;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;

namespace EByahe.Application.Events;

public sealed class IncomingEventProcessor(IIncomingRepository incoming, IEventSettings settings, TimeProvider clock)
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  public async Task ProcessAsync(IEventConsumer consumer, ConsumedRecord record, CancellationToken cancellationToken)
  {
    // Persist the original envelope before attempting deserialization or business work.
    var id = await incoming.ReceiveAsync(settings.ConsumerGroup, record, Now(), cancellationToken);
    if (await incoming.BeginProcessingAsync(id, Now(), cancellationToken))
    {
      try
      {
        var message = ReadRegistration(record);
        await incoming.CompleteAsync(id, message.EventId, new RefUser
        {
          Id = message.User.Id, FirstName = message.User.FirstName, LastName = message.User.LastName,
          Email = message.User.Email, Provider = message.User.Provider, Role = message.User.Role,
          CreatedAtUtc = message.OccurredAtUtc
        }, Now(), cancellationToken);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
      catch (Exception exception)
      {
        var error = exception is InvalidDataException or JsonException
          ? "Invalid or unsupported user.registered v1 event."
          : "User projection failed; the Kafka offset was not committed.";
        await incoming.FailAsync(id, error, Now(), cancellationToken);
        throw;
      }
    }

    // A redelivery after SQL commit skips projection and retries only acknowledgement.
    try { consumer.Commit(record); }
    catch
    {
      await incoming.AppendLogAsync(id, "AcknowledgementFailed",
        "Kafka offset commit failed; the processed record may be delivered again.", Now(), cancellationToken);
      throw;
    }
    await incoming.AppendLogAsync(id, "Acknowledged", null, Now(), cancellationToken);
  }

  private DateTime Now() => clock.GetUtcNow().UtcDateTime;

  private static UserRegisteredEvent ReadRegistration(ConsumedRecord record)
  {
    if (string.IsNullOrWhiteSpace(record.Payload)) throw new InvalidDataException("Missing event payload.");
    var message = JsonSerializer.Deserialize<UserRegisteredEvent>(record.Payload, JsonOptions);
    if (message is null || message.EventId == Guid.Empty || message.EventType != "user.registered"
      || message.SchemaVersion != 1 || message.OccurredAtUtc == default || message.User is null)
      throw new InvalidDataException("Unsupported event envelope.");
    var user = message.User;
    if (!Valid(user.Id, RefUser.IdMaxLength) || !Valid(user.FirstName, RefUser.NameMaxLength)
      || !Valid(user.LastName, RefUser.NameMaxLength) || !Valid(user.Email, RefUser.EmailMaxLength)
      || !MailAddress.TryCreate(user.Email, out _) || !Valid(user.Provider, RefUser.ProviderMaxLength)
      || user.Role is not (RefUser.AdministratorRole or RefUser.NormalRole)
      || !string.Equals(record.Key, user.Id, StringComparison.Ordinal))
      throw new InvalidDataException("Invalid registered user.");
    return message;
  }

  private static bool Valid(string? value, int maxLength) =>
    !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;
}
