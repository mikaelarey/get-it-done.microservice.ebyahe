using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Application.Dtos.Responses;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private async Task SeedReviewOwnerAsync()
  {
    await using var db = CreateDatabase();
    db.RefUsers.Add(MembershipUser(PartnerApiFactory.Subject));
    await db.SaveChangesAsync();
  }

  [SqlServerFact]
  public async Task ApprovalCreatesOnePartnerOwnerMembershipAndCompactRoleEventForEachBusinessCategory()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    foreach (var category in new[] { "bus-operator", "hotel" })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
      using var form = ApplicationForm(category); using var submitted = await client.PostAsync("/api/partner-applications", form);
      var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
      var url = $"/api/admin/partner-applications/{receipt.Id}";
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "approval-reviewer", resourceAccess: ReviewRoles()));
      using var started = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, started.StatusCode);
      await using (var edit = CreateDatabase())
      {
        var application = await edit.PartnerApplications.SingleAsync(x => x.Id == receipt.Id);
        application.BusinessName = new string(category == "hotel" ? 'H' : 'B', 255); await edit.SaveChangesAsync();
      }
      var approvals = await Task.WhenAll(client.PostAsync(url + "/approve", null), client.PostAsync(url + "/approve", null));
      foreach (var response in approvals) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); response.Dispose(); }
      await using var db = CreateDatabase();
      var partner = await db.Partners.Include(x => x.UserMemberships).SingleAsync(x => x.PartnerApplicationId == receipt.Id);
      Assert.Equal(PartnerApiFactory.Subject, partner.OwnerUserId); Assert.Equal("Approved", partner.Status); Assert.False(partner.IsDemo);
      Assert.Equal(255, partner.DisplayName.Length);
      Assert.Equal("PT-" + receipt.Id.ToString("N").ToUpperInvariant(), partner.Code);
      var membership = Assert.Single(partner.UserMemberships); Assert.True(membership.IsActive);
      Assert.Equal((await db.RefUsers.SingleAsync()).Id, membership.RefUserId);
      var messages = await db.OutboxMessages.Where(x => x.EventType == "partner.approved").ToArrayAsync();
      var message = Assert.Single(messages, x => JsonDocument.Parse(x.Payload).RootElement.GetProperty("partner").GetProperty("id").GetGuid() == partner.Id);
      Assert.Equal("get-it-done.ebyahe.partners.v1", message.Topic); Assert.Equal("Pending", message.Status);
      Assert.Equal(PartnerApiFactory.Subject, message.MessageKey); Assert.Equal("approval-reviewer", message.UserId);
      using var json = JsonDocument.Parse(message.Payload); var root = json.RootElement;
      Assert.Equal(message.Id, root.GetProperty("eventId").GetGuid()); Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
      Assert.Equal("ebyahe", root.GetProperty("source").GetString());
      var data = root.GetProperty("partner"); Assert.Equal(receipt.Id, data.GetProperty("applicationId").GetGuid());
      Assert.Equal(category, data.GetProperty("businessCategory").GetString()); Assert.Equal(PartnerApiFactory.Subject, data.GetProperty("ownerUserId").GetString());
      Assert.True(System.Text.Encoding.UTF8.GetByteCount(message.Payload) < 2048);
      foreach (var secret in new[] { "accountNumber", "storageKey", "representativeEmail", "password", "roles" }) Assert.DoesNotContain(secret, message.Payload);
      Assert.Single(await db.Outgoing.Where(x => x.MessageId == message.Id && x.Status == "Pending").ToArrayAsync());
      Assert.Single(await db.PartnerApplicationApprovals.Where(x => x.PartnerApplicationId == receipt.Id && x.Status == "Approved").ToArrayAsync());
    }
    await using var check = CreateDatabase(); Assert.Equal(2, await check.Partners.CountAsync()); Assert.Equal(2, await check.PartnerUsers.CountAsync());
  }

  [SqlServerFact]
  public async Task ApprovalWaitsForTheExactCreatorReferenceAndDeclineCreatesNoPartner()
  {
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
    var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    var url = $"/api/admin/partner-applications/{receipt.Id}";
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: ReviewRoles()));
    using var started = await client.PostAsync(url + "/review", null); Assert.Equal(HttpStatusCode.OK, started.StatusCode);
    using var blocked = await client.PostAsync(url + "/approve", null); Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    Assert.Contains("account synchronization", await blocked.Content.ReadAsStringAsync());
    await using var db = CreateDatabase();
    Assert.Equal("UnderReview", (await db.PartnerApplications.AsNoTracking().SingleAsync()).Status);
    Assert.Empty(await db.Partners.ToArrayAsync()); Assert.Empty(await db.PartnerUsers.ToArrayAsync());
    Assert.DoesNotContain(await db.OutboxMessages.ToArrayAsync(), x => x.EventType == "partner.approved");
    using var declined = await client.PostAsJsonAsync(url + "/decline", new { remarks = "Incomplete documents" });
    Assert.Equal(HttpStatusCode.OK, declined.StatusCode);
    Assert.Empty(await db.Partners.ToArrayAsync()); Assert.DoesNotContain(await db.OutboxMessages.ToArrayAsync(), x => x.EventType == "partner.approved");
  }

  [SqlServerFact]
  public async Task FailedRoleOutboxInsertRollsBackPartnerMembershipApprovalAndApplicationStatus()
  {
    await SeedReviewOwnerAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    using var form = ApplicationForm(); using var submitted = await client.PostAsync("/api/partner-applications", form);
    var receipt = (await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    var url = $"/api/admin/partner-applications/{receipt.Id}";
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "reviewer", resourceAccess: ReviewRoles()));
    using var started = await client.PostAsync(url + "/review", null);
    await using var db = CreateDatabase();
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectPartnerApprovalEvent ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted WHERE EventType = 'partner.approved') THROW 51050, 'Test outbox failure', 1;");
    using var response = await client.PostAsync(url + "/approve", null); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Empty(await db.Partners.ToArrayAsync()); Assert.Empty(await db.PartnerUsers.ToArrayAsync());
    Assert.Equal("UnderReview", (await db.PartnerApplications.AsNoTracking().SingleAsync()).Status);
    Assert.Equal(2, await db.PartnerApplicationApprovals.CountAsync()); Assert.Equal(2, await db.PartnerApplicationHistories.CountAsync());
    Assert.DoesNotContain(await db.OutboxMessages.ToArrayAsync(), x => x.EventType == "partner.approved");
  }
}
