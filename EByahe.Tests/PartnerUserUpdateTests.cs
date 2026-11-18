using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Messaging;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace EByahe.Tests;
public sealed partial class SqlServerDatabaseTests {
  [SqlServerFact]
  public async Task PartnerUserEditRequiresExactManageRoleAndScopedActiveApprovedMembership() {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var member = new PartnerUser { PartnerId = partner.Id, RefUser = MembershipUser("editable-member") };
    var foreign = new PartnerUser { Partner = new Partner { Code = "USER-UPDATE-FOREIGN", DisplayName = "Foreign" }, RefUser = member.RefUser };
    db.PartnerUsers.AddRange(member, foreign); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var path = "/api/partner/users/" + member.Id;
    foreach (var role in new[] { "member.create", "booking.read" }) {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles(role)));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "/account-access")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "/edit")).StatusCode);
      Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(path, new { isActive = false })).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("member.manage")));
    using var response = await client.GetAsync(path + "/account-access"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var access = (await response.Content.ReadFromJsonAsync<PartnerUserAccountAccess>())!; Assert.Equal("editable-member", access.ExternalUserId); Assert.True(access.CanDeactivate);
    var owner = await db.PartnerUsers.SingleAsync(x => x.RefUser.ExternalUserId == PartnerApiFactory.Subject);
    var ownerAccess = await client.GetFromJsonAsync<PartnerUserAccountAccess>($"/api/partner/users/{owner.Id}/account-access");
    Assert.False(ownerAccess!.CanDeactivate); Assert.True(ownerAccess.ProtectManagementPermissions);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/users/{foreign.Id}/account-access")).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "stranger", resourceAccess: BusRoles("member.manage")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/account-access")).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("member.manage")));
    owner.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "/edit")).StatusCode);
  }
  [SqlServerFact]
  public async Task PartnerAccountReplyUpdatesMembershipAndProfileOnceAndIgnoresOlderReplies() {
    var partner = await SeedMemberPartner(); Guid memberId;
    await using (var seed = CreateDatabase()) { var member = new PartnerUser { PartnerId = partner.Id, RefUser = MembershipUser("updated-member") };
      seed.PartnerUsers.Add(member); await seed.SaveChangesAsync(); memberId = member.Id; }
    var now = DateTimeOffset.UtcNow;
    ConsumedRecord Reply(DateTimeOffset time, bool active, string name, Guid? target = null) {
      var id = Guid.NewGuid(); return new(new KafkaOptions().PartnerMembersTopic, 0, 1, id.ToString(), JsonSerializer.Serialize(new {
        eventId = id, eventType = "partner.member.updated", schemaVersion = 1, source = "account", occurredAtUtc = time,
        createdBy = PartnerApiFactory.Subject, partnerId = partner.Id, membershipId = target ?? memberId, isActive = active,
        user = new { id = "updated-member", firstName = name, lastName = "Member", email = "updated@example.com" }
      }));
    }
    async Task Apply(ConsumedRecord record) { await using var db = CreateDatabase(); await new PartnerMemberRepository(db, Options.Create(new KafkaOptions()), MemberPasswords()).ApplyAccountUpdateAsync(record, default); }
    var reply = Reply(now, false, "Updated"); await Apply(reply); await Apply(reply);
    await Apply(Reply(now.AddSeconds(-1), true, "Old"));
    await using var check = CreateDatabase(); var saved = await check.PartnerUsers.Include(x => x.RefUser).SingleAsync(x => x.Id == memberId);
    Assert.False(saved.IsActive); Assert.Equal(now, saved.LastAccountUpdateAtUtc); Assert.Equal("Updated", saved.RefUser.FirstName);
    var notification = await check.OutboxMessages.SingleAsync(x => x.EventType == "partner-user.updated");
    Assert.Contains("Updated a partner user.", notification.Payload); Assert.Contains(PartnerApiFactory.Subject, notification.Payload);
    using var json = JsonDocument.Parse(notification.Payload); Assert.True(json.RootElement.GetProperty("details").GetProperty("previousIsActive").GetBoolean());
    Assert.False(json.RootElement.GetProperty("details").GetProperty("isActive").GetBoolean());
    await Assert.ThrowsAsync<InvalidDataException>(() => Apply(Reply(now.AddSeconds(1), true, "Invalid", Guid.NewGuid())));
  }
}
