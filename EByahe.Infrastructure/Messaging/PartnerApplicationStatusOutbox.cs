using EByahe.Domain.Models;

namespace EByahe.Infrastructure.Messaging;

internal static class PartnerApplicationStatusOutbox
{
  public static OutboxMessage Create(PartnerApplication application, PartnerApplicationApproval decision, string topic)
  {
    var (eventType, message) = decision.Status switch
    {
      "UnderReview" => ("partner-application.under-review", "Started reviewing a partner application."),
      "Approved" => ("partner-application.approved", "Approved a partner application."),
      "Rejected" => ("partner-application.declined", "Declined a partner application."),
      _ => throw new ArgumentOutOfRangeException(nameof(decision))
    };
    // Reuse the immutable decision ID for deduplication. The reviewer is the actor;
    // the original applicant is the explicit recipient, even when they differ.
    return ActivityOutbox.Create(new ActivityEvent(decision.Id, eventType, 1, decision.DateTimeCreated,
      "ebyahe", decision.ChangedBy, message, "/partner-application-details/" + application.Id.ToString("D"),
      Details: new { application.Id, application.TrackingId, decision.PreviousStatus, decision.Status },
      Audiences: new[] { new { kind = "User", externalUserId = application.CreatedBy } }), topic);
  }
}
