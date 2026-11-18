using System.Net.Mail;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using EByahe.Domain.Repositories;

namespace EByahe.Application.Events;

public sealed class IncomingEventProcessor(IIncomingRepository incoming, IEventSettings settings, TimeProvider clock,
  IPartnerMemberRepository? members = null, IBookingPaymentRepository? payments = null)
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
        if (record.Topic == settings.PaymentCompletedTopic)
        {
          var payment = JsonSerializer.Deserialize<PaymentCompletedEvent>(record.Payload ?? "", JsonOptions);
          if (payment is null || payment.EventId == Guid.Empty || payment.PaymentId == Guid.Empty || payment.BookingId == Guid.Empty
            || (payment.EventType is not ("payment.completed" or "payment.failed" or "payment.cancelled")) || payment.SchemaVersion != 1
            || (payment.EventType == "payment.completed" ? payment.PaymentStatus != "Payment Success" : payment.PaymentStatus != (payment.EventType == "payment.cancelled" ? "Cancelled" : "Payment Failed"))
            || payment.PaymentType is not (null or "GCash" or "Maya" or "Credit/Debit Card") || payment.OccurredAtUtc == default
            || payment.Provider != "paymongo" || payment.Currency != "PHP" || payment.AmountCentavos < 100
            || string.IsNullOrWhiteSpace(payment.OwnerId) || payment.OwnerId.Length > 255
            || payment.BookingReference is not { Length: 35 } || !payment.BookingReference.StartsWith("BK-", StringComparison.Ordinal)
            || payment.EventType != "payment.cancelled" && (payment.ProviderSessionId is not { Length: > 3 and <= 100 } || !payment.ProviderSessionId.StartsWith("cs_", StringComparison.Ordinal)
              || payment.ProviderPaymentId is not { Length: > 4 and <= 100 } || !payment.ProviderPaymentId.StartsWith("pay_", StringComparison.Ordinal))
            || record.Key != payment.BookingId.ToString()) throw new InvalidDataException("Invalid payment.completed v1 event.");
          await (payments ?? throw new InvalidOperationException("Payment repository not configured."))
            .CompleteAsync(id, payment, Now(), cancellationToken);
        }
        else if (record.Topic == settings.PartnerMembersTopic)
        {
          using var json = JsonDocument.Parse(record.Payload ?? "");
          var type = json.RootElement.GetProperty("eventType").GetString();
          var eventId = json.RootElement.GetProperty("eventId").GetGuid();
          if (eventId == Guid.Empty || record.Key != eventId.ToString()) throw new InvalidDataException();
          if (type == "partner.member.provisioned")
            await (members ?? throw new InvalidOperationException()).CompleteAsync(record, cancellationToken);
          else if (type == "partner.member.updated")
            await (members ?? throw new InvalidOperationException()).ApplyAccountUpdateAsync(record, cancellationToken);
          else if (type != "partner.member.requested") throw new InvalidDataException("Unsupported partner member event.");
          await incoming.CompleteRecordAsync(id, eventId, Now(), cancellationToken);
        }
        else
        {
        var message = ReadRegistration(record);
        await incoming.CompleteAsync(id, message.EventId, new RefUser
        {
          ExternalUserId = message.User.Id, FirstName = message.User.FirstName, LastName = message.User.LastName,
          Email = message.User.Email, Provider = message.User.Provider, Role = message.User.Role,
          CreatedAtUtc = message.OccurredAtUtc
        }, Now(), cancellationToken);
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
      catch (Exception exception)
      {
        var error = exception is InvalidDataException or JsonException
          ? "Invalid or unsupported incoming event."
          : "Incoming event processing failed; the Kafka offset was not committed.";
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
    if (!Valid(user.Id, RefUser.ExternalUserIdMaxLength) || !Valid(user.FirstName, RefUser.NameMaxLength)
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
