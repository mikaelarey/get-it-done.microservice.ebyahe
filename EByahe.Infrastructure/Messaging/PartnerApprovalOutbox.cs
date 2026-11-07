using EByahe.Domain.Models;

namespace EByahe.Infrastructure.Messaging;

internal static class PartnerApprovalOutbox
{
  public const string DefaultTopic = "get-it-done.ebyahe.partners.v1";

  public static OutboxMessage Create(Partner partner, PartnerApplication application, string reviewer, string topic) =>
    ActivityOutbox.Create(new ActivityEvent(Guid.NewGuid(), "partner.approved", 1, DateTimeOffset.UtcNow,
      "ebyahe", reviewer, "Approved a partner application.", "/application-details/" + application.Id,
      Partner: new { partner.Id, ApplicationId = application.Id, partner.OwnerUserId,
        application.BusinessCategory, partner.DisplayName, partner.Status }), topic, partner.OwnerUserId);
}
