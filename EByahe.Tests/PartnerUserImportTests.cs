using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Application.Services;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Infrastructure.Repositories;
using EByahe.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace EByahe.Tests;
public sealed partial class SqlServerDatabaseTests {
  private static MultipartFormDataContent UserUpload(Guid partnerId, Guid uploadId, params string[][] rows) {
    var body = new MultipartFormDataContent(); body.Add(new StringContent(partnerId.ToString()), "partnerId"); body.Add(new StringContent(uploadId.ToString()), "uploadId");
    body.Add(new ByteArrayContent(UserExcelImportTests.Workbook(rows)), "file", "users.xlsx"); return body;
  }
  [SqlServerFact]
  public async Task UserExcelUploadQueuesOnceLinksCompletedRecordsAndSendsOneUploadNotificationAfterAllReplies() {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles()));
    var uploadId = Guid.NewGuid(); string[][] rows = [["First", "One", "first@example.com"], ["Second", "Two", "second@example.com"]];
    using (var template = await client.GetAsync("/api/partner/users/template")) { Assert.Equal(HttpStatusCode.OK, template.StatusCode); Assert.Equal(UserExcelTemplate.FileName, template.Content.Headers.ContentDisposition!.FileNameStar); }
    async Task<HttpResponseMessage> Submit() { using var file = UserUpload(partner.Id, uploadId, rows); return await client.PostAsync("/api/partner/users/import", file); }
    using var response = await Submit(); Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    var result = (await response.Content.ReadFromJsonAsync<PartnerUserImportResult>())!; Assert.Equal(2, result.QueuedCount); Assert.Equal(uploadId, result.UploadId);
    using var repeated = await Submit(); Assert.Equal(HttpStatusCode.Accepted, repeated.StatusCode);
    await using var db = CreateDatabase(); Assert.Equal(1, await db.Uploads.CountAsync()); Assert.Equal(2, await db.PartnerMemberRequests.CountAsync());
    Assert.Equal(2, await db.UploadMemberRequests.CountAsync()); Assert.Equal(1, await db.PartnerUsers.CountAsync()); Assert.Empty(await db.UploadRecords.ToArrayAsync());
    Assert.Empty(await db.OutboxMessages.Where(x => x.EventType == "users.imported").ToArrayAsync());
    var detail = (await client.GetFromJsonAsync<UploadDetails>($"/api/partner/uploads/{uploadId}"))!;
    Assert.Equal(2, detail.UserProgress!.PendingCount); Assert.Equal(0, detail.TotalCount);
    var requests = await db.PartnerMemberRequests.OrderBy(x => x.Email).ToArrayAsync();
    foreach (var message in await db.OutboxMessages.Where(x => x.EventType == "partner.member.requested").ToArrayAsync()) {
      using var payload = JsonDocument.Parse(message.Payload);
      Assert.Equal(new[] { "booking.read", "bus.read", "terminal.read" }, payload.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
    }
    async Task Complete(PartnerMemberRequest request, string subject) { await using var context = CreateDatabase(); await new PartnerMemberRepository(context, Options.Create(new KafkaOptions()), MemberPasswords()).CompleteAsync(MemberReply(request, true, subject), default); }
    await Complete(requests[0], "bulk-user-one");
    Assert.Single(await db.UploadRecords.ToArrayAsync()); Assert.Empty(await db.OutboxMessages.Where(x => x.EventType == "users.imported").ToArrayAsync());
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectUserImportSummary ON OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.CodeDecodeEventTypes et ON et.Code=i.CodeDecodeEventType WHERE et.Decode='users.imported') THROW 51065, 'Test summary failure', 1;");
    await Assert.ThrowsAsync<DbUpdateException>(() => Complete(requests[1], "bulk-user-two"));
    Assert.Single(await db.UploadRecords.ToArrayAsync()); Assert.Equal(2, await db.PartnerUsers.CountAsync());
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectUserImportSummary");
    await Complete(requests[1], "bulk-user-two"); await Complete(requests[1], "bulk-user-two");
    Assert.Equal(2, await db.UploadRecords.CountAsync()); Assert.Equal(3, await db.PartnerUsers.CountAsync());
    var summary = await db.OutboxMessages.SingleAsync(x => x.EventType == "users.imported"); using var json = JsonDocument.Parse(summary.Payload);
    Assert.Equal("Successfully created 2 users using file upload.", json.RootElement.GetProperty("message").GetString());
    Assert.Equal("/uploads/" + uploadId, json.RootElement.GetProperty("link").GetString());
    detail = (await client.GetFromJsonAsync<UploadDetails>($"/api/partner/uploads/{uploadId}"))!; Assert.Equal(2, detail.TotalCount); Assert.Equal(2, detail.Users!.Count);
    Assert.Equal(0, detail.UserProgress!.PendingCount); Assert.Equal(2, detail.UserProgress.CompletedCount); Assert.NotNull(detail.UserProgress.FinishedAtUtc);
    Assert.DoesNotContain("password", await client.GetStringAsync($"/api/partner/uploads/{uploadId}"), StringComparison.OrdinalIgnoreCase);
    using var password = await client.GetAsync($"/api/partner/users/requests/{requests[0].Id}/temporary-password"); Assert.Equal(HttpStatusCode.OK, password.StatusCode);
    using var changed = UserUpload(partner.Id, uploadId, ["Changed", "Name", "changed@example.com"]);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/partner/users/import", changed)).StatusCode);
  }
  [SqlServerFact]
  public async Task UserExcelUploadRejectsAllRowsOnInvalidDuplicateActiveOrPendingEmailsAndKeepsAttemptLogs() {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles()));
    using var invalid = UserUpload(partner.Id, Guid.NewGuid(), ["Good", "User", "good@example.com"], ["Bad", "User", "not-an-email"]);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/partner/users/import", invalid)).StatusCode);
    using var duplicate = UserUpload(partner.Id, Guid.NewGuid(), ["One", "User", "one@example.com"], ["Two", "User", "ONE@example.com"]);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/partner/users/import", duplicate)).StatusCode);
    await using var db = CreateDatabase(); var existing = MembershipUser("existing-upload-member"); existing.Email = "existing@example.com";
    db.PartnerUsers.Add(new PartnerUser { PartnerId = partner.Id, RefUser = existing }); await db.SaveChangesAsync();
    using var active = UserUpload(partner.Id, Guid.NewGuid(), ["New", "User", "new@example.com"], ["Existing", "User", "existing@example.com"]);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/partner/users/import", active)).StatusCode);
    Assert.Empty(await db.PartnerMemberRequests.ToArrayAsync()); Assert.Empty(await db.UserUploadBatches.ToArrayAsync()); Assert.Equal(3, await db.Uploads.CountAsync());
    using var pending = UserUpload(partner.Id, Guid.NewGuid(), ["New", "User", "new@example.com"]);
    Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/partner/users/import", pending)).StatusCode);
    using var blocked = UserUpload(partner.Id, Guid.NewGuid(), ["New", "User", "new@example.com"]);
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/partner/users/import", blocked)).StatusCode);
    Assert.Single(await db.PartnerMemberRequests.ToArrayAsync());
  }
  [SqlServerFact]
  public async Task UserExcelUploadRequiresCreateRoleAndActiveWorkspaceBeforeRegisteringAttempt() {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    foreach (var roles in new object?[] { null, BusRoles("member.manage"), BusRoles("booking.read") }) {
      client.DefaultRequestHeaders.Authorization = roles is null ? null : new("Bearer", factory.Token(resourceAccess: roles));
      using var file = UserUpload(partner.Id, Guid.NewGuid(), ["New", "User", "new@example.com"]);
      Assert.Equal(roles is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/users/import", file)).StatusCode);
      Assert.Equal(roles is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/users/template")).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles()));
    using var foreign = UserUpload(Guid.NewGuid(), Guid.NewGuid(), ["New", "User", "new@example.com"]);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/users/import", foreign)).StatusCode);
    await using var db = CreateDatabase(); var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    using var inactive = UserUpload(partner.Id, Guid.NewGuid(), ["New", "User", "new@example.com"]);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/users/import", inactive)).StatusCode);
    Assert.Empty(await db.Uploads.ToArrayAsync()); Assert.Empty(await db.UserUploadBatches.ToArrayAsync());
  }
}
