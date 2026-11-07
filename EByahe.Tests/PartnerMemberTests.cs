using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Messaging;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static object MemberRoles(bool partner = true, bool create = true) => new Dictionary<string, object> {
    ["get-it-done-web"] = new { roles = partner ? new[] { "partner" } : new[] { "admin" } },
    ["get-it-done-ebyahe-api"] = new { roles = create ? new[] { "member.create" } : new[] { "booking.read" } } };
  private static IPartnerPasswordProtector MemberPasswords() => new PartnerPasswordProtector(new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["PartnerMembers:PasswordEncryptionKey"] = Convert.ToBase64String(new byte[32]) }).Build());
  private async Task<Partner> SeedMemberPartner(string category = "bus-operator")
  {
    await using var db = CreateDatabase();
    var application = new PartnerApplication { Id = Guid.NewGuid(), CreatedBy = PartnerApiFactory.Subject,
      TrackingId = "APP-" + Guid.NewGuid().ToString("N"), Status = "Approved", BusinessCategory = category,
      EntityType = "corporation", SettlementFrequency = "daily", BusinessName = "Member company", SubmittedAtUtc = DateTimeOffset.UtcNow };
    var partner = new Partner { Code = "MEMBERS-" + Guid.NewGuid().ToString("N"), DisplayName = "Member company",
      OwnerUserId = PartnerApiFactory.Subject, PartnerApplicationId = application.Id };
    db.PartnerApplications.Add(application); db.Partners.Add(partner);
    db.PartnerUsers.Add(new PartnerUser { Partner = partner, RefUser = MembershipUser(PartnerApiFactory.Subject) });
    await db.SaveChangesAsync(); return partner;
  }

  [SqlServerFact]
  public async Task PartnerMemberCreationRequiresBothExactClientRolesAndActiveScopedMembership()
  {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var command = new { requestId = Guid.NewGuid(), partnerId = partner.Id, firstName = "New", lastName = "Member", email = "new@example.com" };
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/partner/users", command)).StatusCode);
    foreach (var roles in new[] { MemberRoles(create: false), MemberRoles(partner: false) })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/users", command)).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/users/workspaces")).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "stranger", resourceAccess: MemberRoles()));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/users", command)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles()));
    Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/partner/users", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/partner/users", command with { partnerId = Guid.NewGuid(), requestId = Guid.NewGuid() })).StatusCode);
  }

  [SqlServerFact]
  public async Task PartnerMemberRequestsAreEncryptedIdempotentAndCompleteMembershipOnlyAfterAccountReply()
  {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles()));
    var id = Guid.NewGuid(); var command = new { requestId = id, partnerId = partner.Id, firstName = "New", lastName = "Member", email = "NEW@example.com" };
    var response = await client.PostAsJsonAsync("/api/partner/users", command); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    Assert.True(response.Headers.CacheControl!.NoStore); Assert.DoesNotContain("\"password\":", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/partner/users", command)).StatusCode);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/users", new { requestId = id, partnerId = partner.Id, firstName = "Changed", lastName = "Member", email = command.email })).StatusCode);
    await using var db = CreateDatabase(); var request = await db.PartnerMemberRequests.SingleAsync();
    Assert.Equal("Pending", request.Status); Assert.Single(await db.PartnerUsers.ToArrayAsync());
    var temporary = await db.TemporaryPasswords.SingleAsync(); var password = MemberPasswords().Unprotect(id, temporary.ProtectedPassword);
    Assert.Equal(10, password.Length); Assert.Contains(password, char.IsUpper); Assert.Contains(password, char.IsLower); Assert.Contains(password, char.IsDigit);
    var outgoing = await db.OutboxMessages.SingleAsync(x => x.EventType == "partner.member.requested");
    Assert.DoesNotContain(password, outgoing.Payload); Assert.Contains("bus-operator", outgoing.Payload);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/users/requests/{id}/temporary-password")).StatusCode);
    var repo = new PartnerMemberRepository(db, Options.Create(new KafkaOptions()), MemberPasswords());
    var record = MemberReply(request, true);
    await repo.CompleteAsync(record, default); await repo.CompleteAsync(record, default);
    Assert.Equal(2, await db.PartnerUsers.CountAsync()); Assert.Equal(2, await db.RefUsers.CountAsync());
    var status = await client.GetFromJsonAsync<PartnerMemberStatus>($"/api/partner/users/requests/{id}");
    Assert.Equal("Completed", status!.Status); Assert.True(status.HasTemporaryPassword);
    var users = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalUser>>("/api/partner/users"))!;
    Assert.Equal(id, Assert.Single(users.Items, row => row.TemporaryPasswordRequestId != null).TemporaryPasswordRequestId);
    Assert.DoesNotContain(password, await client.GetStringAsync("/api/partner/users"));
    var revealed = await client.GetFromJsonAsync<PartnerTemporaryPassword>($"/api/partner/users/requests/{id}/temporary-password"); Assert.Equal(password, revealed!.Password);
    Assert.Single((await client.GetFromJsonAsync<PartnerMemberStatus[]>("/api/partner/users/requests"))!);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "new-member", resourceAccess: MemberRoles()));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/users/requests/{id}/temporary-password")).StatusCode);
    Assert.Empty((await client.GetFromJsonAsync<PartnerMemberStatus[]>("/api/partner/users/requests"))!);
    Assert.All((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalUser>>("/api/partner/users"))!.Items,
      row => Assert.Null(row.TemporaryPasswordRequestId));
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles(create: false)));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/partner/users/requests/{id}/temporary-password")).StatusCode);
    Assert.All((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalUser>>("/api/partner/users"))!.Items,
      row => Assert.Null(row.TemporaryPasswordRequestId));
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles()));
    await db.TemporaryPasswords.Where(row => row.RequestId == id)
      .ExecuteUpdateAsync(update => update.SetProperty(row => row.AvailableUntilUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
    Assert.All((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerPortalUser>>("/api/partner/users"))!.Items,
      row => Assert.Null(row.TemporaryPasswordRequestId));
    Assert.False(db.Database.HasPendingModelChanges());
  }

  [SqlServerFact]
  public async Task ExistingMemberKeepsProfileAndNoTemporaryPasswordIsExposedAndRevokedInviterCancelsRequest()
  {
    var partner = await SeedMemberPartner("hotel"); await using var db = CreateDatabase();
    var existing = MembershipUser("existing-user"); existing.Email = "new@example.com"; existing.FirstName = "Original";
    db.RefUsers.Add(existing); db.PartnerUsers.Add(new PartnerUser { PartnerId = partner.Id, RefUser = existing, IsActive = false }); await db.SaveChangesAsync();
    var repo = new PartnerMemberRepository(db, Options.Create(new KafkaOptions()), MemberPasswords());
    var command = new PartnerMemberCommand(Guid.NewGuid(), partner.Id, "New", "Name", existing.Email);
    await repo.QueueAsync(PartnerApiFactory.Subject, command, default);
    var request = await db.PartnerMemberRequests.SingleAsync(); Assert.Equal("hotel", request.BusinessCategory);
    await repo.CompleteAsync(MemberReply(request, false, existing.ExternalUserId), default);
    Assert.Empty(await db.TemporaryPasswords.ToArrayAsync());
    Assert.Equal("Original", (await db.RefUsers.SingleAsync(x => x.Id == existing.Id)).FirstName);
    Assert.True((await db.PartnerUsers.SingleAsync(x => x.RefUserId == existing.Id)).IsActive);
    var second = command with { RequestId = Guid.NewGuid(), Email = "another@example.com" };
    await repo.QueueAsync(PartnerApiFactory.Subject, second, default);
    var pending = await db.PartnerMemberRequests.SingleAsync(x => x.Id == second.RequestId);
    var inviter = await db.PartnerUsers.SingleAsync(x => x.RefUser.ExternalUserId == PartnerApiFactory.Subject); inviter.IsActive = false; await db.SaveChangesAsync();
    await repo.CompleteAsync(MemberReply(pending, true), default);
    Assert.Equal("Cancelled", (await db.PartnerMemberRequests.FindAsync(second.RequestId))!.Status);
    Assert.Empty(await db.TemporaryPasswords.ToArrayAsync()); Assert.Equal(2, await db.PartnerUsers.CountAsync());
  }
  private static ConsumedRecord MemberReply(PartnerMemberRequest request, bool created, string subject = "new-member") => new(
    "get-it-done.ebyahe.partner-members.v1", 0, 1, request.Id.ToString(), JsonSerializer.Serialize(new {
      eventId = request.Id, eventType = "partner.member.provisioned", schemaVersion = 1, occurredAtUtc = DateTimeOffset.UtcNow,
      source = "account", request.PartnerId, request.CreatedBy, request.RequestHash, temporaryPasswordAssigned = created,
      user = new { id = subject, firstName = "New", lastName = "Member", request.Email, provider = RefUser.ManualProvider,
        role = RefUser.NormalRole, createdAtUtc = DateTimeOffset.UtcNow } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
}
