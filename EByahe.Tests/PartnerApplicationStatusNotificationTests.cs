using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EByahe.Application.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ReviewAndFinalDecisionsQueueApplicantOnlyNotificationsOncePerTransition()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    foreach (var action in new[] { "approve", "decline" })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
      using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
      Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
      var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
      var url = $"/api/admin/partner-applications/{receipt.Id}";
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "status-reviewer", resourceAccess: ReviewRoles()));
      using var premature = await client.PostAsJsonAsync(url + "/" + action, new { remarks = "Private decision remarks" });
      Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
      using var review = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, review.StatusCode);
      using var retryReview = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, retryReview.StatusCode);
      using var decided = await client.PostAsJsonAsync(url + "/" + action, new { remarks = "Private decision remarks" });
      Assert.Equal(HttpStatusCode.OK, decided.StatusCode);
      using var retry = await client.PostAsJsonAsync(url + "/" + action, new { remarks = "Retry remarks" });
      Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
      using var conflict = await client.PostAsJsonAsync(url + (action == "approve" ? "/decline" : "/approve"), new { remarks = "Conflict" });
      Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

      await using var db = CreateDatabase();
      var decisions = await db.PartnerApplicationApprovals.Where(x => x.PartnerApplicationId == receipt.Id && x.Status != "Pending").ToArrayAsync();
      Assert.Equal(2, decisions.Length);
      var decisionIds = decisions.Select(x => x.Id).ToArray();
      var messages = await db.OutboxMessages.Where(x => decisionIds.Contains(x.Id)).ToArrayAsync();
      Assert.Equal(2, messages.Length);
      foreach (var decision in decisions)
      {
        var message = Assert.Single(messages, x => x.Id == decision.Id);
        var (type, text) = decision.Status switch
        {
          "UnderReview" => ("under-review", "Started reviewing a partner application."),
          "Approved" => ("approved", "Approved a partner application."),
          _ => ("declined", "Declined a partner application.")
        };
        Assert.Equal("partner-application." + type, message.EventType);
        Assert.Equal("get-it-done.notifications.v1", message.Topic);
        Assert.Equal("status-reviewer", message.MessageKey); Assert.Equal("status-reviewer", message.UserId);
        Assert.Equal("Pending", message.Status);
        using var json = JsonDocument.Parse(message.Payload); var root = json.RootElement;
        Assert.Equal(text, root.GetProperty("message").GetString());
        Assert.Equal(decision.Id, root.GetProperty("eventId").GetGuid());
        Assert.Equal(decision.DateTimeCreated, root.GetProperty("occurredAtUtc").GetDateTimeOffset());
        Assert.Equal("/partner-application-details/" + receipt.Id, root.GetProperty("link").GetString());
        var audience = Assert.Single(root.GetProperty("audiences").EnumerateArray());
        Assert.Equal("User", audience.GetProperty("kind").GetString());
        Assert.Equal(PartnerApiFactory.Subject, audience.GetProperty("externalUserId").GetString());
        Assert.Equal(receipt.Id, root.GetProperty("details").GetProperty("id").GetGuid());
        Assert.Equal(decision.Status, root.GetProperty("details").GetProperty("status").GetString());
        Assert.True(Encoding.UTF8.GetByteCount(message.Payload) < 2048);
        foreach (var excluded in new[] { "Private decision remarks", "accountNumber", "storageKey", "representativeEmail" })
          Assert.DoesNotContain(excluded, message.Payload);
        Assert.Single(await db.Outgoing.Where(x => x.MessageId == message.Id && x.Status == "Pending").ToArrayAsync());
      }
    }
  }

  [SqlServerFact]
  public async Task NotificationOutboxFailureRollsBackEachStatusAndCanBeRetried()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    foreach (var action in new[] { "review", "approve", "decline" })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
      using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
      var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
      var url = $"/api/admin/partner-applications/{receipt.Id}";
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "status-reviewer", resourceAccess: ReviewRoles()));
      if (action != "review")
      { using var started = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, started.StatusCode); }
      await using var db = CreateDatabase();
      var before = await db.PartnerApplications.AsNoTracking().SingleAsync(x => x.Id == receipt.Id);
      var approvals = await db.PartnerApplicationApprovals.CountAsync();
      var histories = await db.PartnerApplicationHistories.CountAsync();
      var outbox = await db.OutboxMessages.CountAsync();
      var partners = await db.Partners.CountAsync();
      // All status events share this prefix; submission has already committed.
      await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectStatusNotification ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted WHERE EventType LIKE 'partner-application.%') THROW 51051, 'Test notification failure', 1;");
      using var failed = await client.PostAsJsonAsync(url + "/" + action, new { remarks = "Invalid permit" });
      Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
      Assert.Equal(before.Status, (await db.PartnerApplications.AsNoTracking().SingleAsync(x => x.Id == receipt.Id)).Status);
      Assert.Equal(approvals, await db.PartnerApplicationApprovals.CountAsync());
      Assert.Equal(histories, await db.PartnerApplicationHistories.CountAsync());
      Assert.Equal(outbox, await db.OutboxMessages.CountAsync()); Assert.Equal(partners, await db.Partners.CountAsync());
      await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectStatusNotification");
      using var retried = await client.PostAsJsonAsync(url + "/" + action, new { remarks = "Invalid permit" });
      Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
      Assert.Equal(approvals + 1, await db.PartnerApplicationApprovals.CountAsync());
    }
  }
}
