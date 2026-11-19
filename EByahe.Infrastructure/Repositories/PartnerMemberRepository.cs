using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using EByahe.Domain.Interfaces;
using EByahe.Domain.Exceptions;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Messaging;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace EByahe.Infrastructure.Repositories;

public sealed class PartnerMemberRepository(EByaheDbContext db, IOptions<KafkaOptions> settings,
  IPartnerPasswordProtector passwords, IRecordChangePublisher? changes = null) : IPartnerMemberRepository
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private IQueryable<Partner> Accessible(string userId) => db.Partners.Where(p => p.Status == "Approved"
    && db.PartnerUsers.Any(m => m.PartnerId == p.Id && m.IsActive && m.RefUser.ExternalUserId == userId));
  private IQueryable<PartnerMemberWorkspace> Workspaces(string userId, Guid? partnerId = null) =>
    from partner in Accessible(userId).Where(p => !partnerId.HasValue || p.Id == partnerId.Value)
    join application in db.PartnerApplications on partner.PartnerApplicationId equals application.Id
    where application.BusinessCategory == "bus-operator" || application.BusinessCategory == "hotel"
    orderby partner.DisplayName
    select new PartnerMemberWorkspace(partner.Id, partner.DisplayName, application.BusinessCategory, null);
  public async Task<IReadOnlyList<PartnerMemberWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken) =>
    await Workspaces(userId).ToArrayAsync(cancellationToken);

  public Task<PartnerUserAccountAccess?> AccountAccessAsync(string userId, Guid membershipId, CancellationToken ct) =>
    (from member in db.PartnerUsers.AsNoTracking()
     join application in db.PartnerApplications on member.Partner.PartnerApplicationId equals application.Id
     where member.Id == membershipId && Accessible(userId).Select(partner => partner.Id).Contains(member.PartnerId)
       && (application.BusinessCategory == "bus-operator" || application.BusinessCategory == "hotel")
     select new PartnerUserAccountAccess(member.Id, member.PartnerId, member.RefUser.ExternalUserId, application.BusinessCategory,
       member.RefUser.ExternalUserId != userId && member.RefUser.ExternalUserId != member.Partner.OwnerUserId,
       member.RefUser.ExternalUserId == userId || db.Partners.Any(partner => partner.OwnerUserId == member.RefUser.ExternalUserId && partner.Status == "Approved")))
    .SingleOrDefaultAsync(ct);

  public async Task ApplyAccountUpdateAsync(ConsumedRecord record, CancellationToken ct)
  {
    if (record.Payload is null || Encoding.UTF8.GetByteCount(record.Payload) > 16384) throw new InvalidDataException();
    var message = JsonSerializer.Deserialize<MemberUpdated>(record.Payload, JsonOptions);
    static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && value == value.Trim() && !value.Any(char.IsControl);
    if (message is null || message.EventId == Guid.Empty || message.EventType != "partner.member.updated" || message.SchemaVersion != 1
      || message.Source != "account" || record.Key != message.EventId.ToString() || message.OccurredAtUtc == default
      || message.MembershipId == Guid.Empty || message.PartnerId == Guid.Empty || !Text(message.CreatedBy, 255)
      || message.User is not {} user || !Text(user.Id, 255)
      || !Text(user.FirstName, 255) || !Text(user.LastName, 255)
      || !Text(user.Email, 254) || !System.Net.Mail.MailAddress.TryCreate(user.Email, out var address) || address.Address != user.Email) throw new InvalidDataException();
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var member = await db.PartnerUsers.FromSqlInterpolated($"SELECT * FROM PartnerUsers WITH (UPDLOCK, HOLDLOCK) WHERE Id = {message.MembershipId}")
      .Include(row => row.RefUser).SingleOrDefaultAsync(ct);
    if (member is null || member.PartnerId != message.PartnerId || member.RefUser.ExternalUserId != user.Id) throw new InvalidDataException();
    if (member.LastAccountUpdateAtUtc >= message.OccurredAtUtc) return;
    var previous = member.IsActive; member.IsActive = message.IsActive; member.LastAccountUpdateAtUtc = message.OccurredAtUtc;
    if (!await db.PartnerUsers.AnyAsync(row => row.RefUserId == member.RefUserId && row.Id != member.Id && row.LastAccountUpdateAtUtc > message.OccurredAtUtc, ct)) {
      member.RefUser.FirstName = user.FirstName; member.RefUser.LastName = user.LastName; member.RefUser.Email = user.Email;
    }
    await db.SaveChangesAsync(ct);
    await PartnerCatalogActivityOutbox.QueueMembershipUpdateAsync(db, member.PartnerId, message.CreatedBy, member.Id, previous,
      member.IsActive, settings.Value.NotificationTopic, ct);
    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    if (changes is not null) await changes.PublishAsync(["users", "dashboard"], message.CreatedBy, member.PartnerId, ct);
  }
  private sealed record MemberUpdated(Guid EventId, string EventType, int SchemaVersion, string Source, DateTimeOffset OccurredAtUtc,
    string CreatedBy, Guid MembershipId, Guid PartnerId, bool IsActive, UpdatedProfile User);
  private sealed record UpdatedProfile(string Id, string FirstName, string LastName, string Email);

  public async Task<PartnerMemberStatus?> QueueAsync(string userId, PartnerMemberCommand command, CancellationToken cancellationToken)
  {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    // Serialize requests for one workspace, including browser retries and concurrent invitations.
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {command.PartnerId}")
      .SingleOrDefaultAsync(cancellationToken);
    var workspace = await Workspaces(userId, command.PartnerId).SingleOrDefaultAsync(cancellationToken);
    if (workspace is null) return null;
    if (await db.RefUsers.AnyAsync(user => user.ExternalUserId == userId && user.Email == command.Email, cancellationToken))
      throw new PartnerMemberSelfInvitationException();
    var roles = command.Roles ?? PartnerMemberRoles.Legacy(workspace.BusinessCategory);
    if (!PartnerMemberRoles.Valid(workspace.BusinessCategory, roles)) throw new PartnerMemberRoleValidationException();
    var rolesJson = JsonSerializer.Serialize(roles.Order(StringComparer.Ordinal).ToArray(), JsonOptions);
    bool SameRoles(PartnerMemberRequest value) => (value.RequestedRolesJson
      ?? JsonSerializer.Serialize(PartnerMemberRoles.Legacy(value.BusinessCategory).Order(StringComparer.Ordinal).ToArray(), JsonOptions)) == rolesJson;
    var existing = await db.PartnerMemberRequests.FindAsync([command.RequestId], cancellationToken);
    if (existing is not null)
    {
      if (existing.PartnerId != command.PartnerId || existing.CreatedBy != userId || existing.Email != command.Email
        || existing.FirstName != command.FirstName || existing.LastName != command.LastName || !SameRoles(existing)) throw new PartnerMemberRequestConflictException();
      return Status(existing);
    }
    var pending = await db.PartnerMemberRequests.SingleOrDefaultAsync(x => x.PartnerId == command.PartnerId
      && x.Email == command.Email && x.Status == "Pending", cancellationToken);
    if (pending is not null) { if (!SameRoles(pending)) throw new PartnerMemberRequestConflictException(); return Status(pending); }
    var request = CreateRequest(userId, command, workspace.BusinessCategory);
    await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
    if (changes is not null) await changes.PublishAsync(["users"], userId, command.PartnerId, cancellationToken);
    return Status(request);
  }
  private PartnerMemberRequest CreateRequest(string userId, PartnerMemberCommand command, string category) {
    var roles = command.Roles ?? PartnerMemberRoles.Legacy(category);
    var rolesJson = JsonSerializer.Serialize(roles.Order(StringComparer.Ordinal).ToArray(), JsonOptions);
    var now = DateTimeOffset.UtcNow;
    var protectedPassword = passwords.Protect(command.RequestId, passwords.Generate());
    var payload = JsonSerializer.Serialize(new { eventId = command.RequestId, eventType = "partner.member.requested",
      schemaVersion = 2, occurredAtUtc = now, source = "ebyahe", createdBy = userId, partnerId = command.PartnerId,
      businessCategory = category, command.FirstName, command.LastName, command.Email,
      roles = roles.Order(StringComparer.Ordinal).ToArray(), protectedPassword }, JsonOptions);
    var request = new PartnerMemberRequest { Id = command.RequestId, PartnerId = command.PartnerId, CreatedBy = userId,
      FirstName = command.FirstName, LastName = command.LastName, Email = command.Email, BusinessCategory = category,
      RequestedRolesJson = rolesJson, CreatedAtUtc = now, RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))) };
    db.PartnerMemberRequests.Add(request);
    db.TemporaryPasswords.Add(new TemporaryPassword { RequestId = request.Id, ProtectedPassword = protectedPassword,
      CreatedAtUtc = now, AvailableUntilUtc = now.AddDays(7) });
    var message = new OutboxMessage { Id = request.Id, UserId = userId, EventType = "partner.member.requested",
      Topic = settings.Value.PartnerMembersTopic, MessageKey = request.Id.ToString(), Payload = payload,
      CreatedDateTime = now.UtcDateTime, NextAttemptDateTime = now.UtcDateTime };
    message.History.Add(Outgoing.Record(message, "Pending", now.UtcDateTime)); db.OutboxMessages.Add(message);
    return request;
  }
  public async Task<PartnerUserImportResult?> ImportAsync(string userId, Guid partnerId, IReadOnlyList<PartnerUserImportRow> rows, Guid uploadId, CancellationToken ct) {
    if (rows.Count is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(rows));
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {partnerId}").SingleOrDefaultAsync(ct);
    var workspace = await Workspaces(userId, partnerId).SingleOrDefaultAsync(ct); if (workspace is null) return null;
    if (!await db.Uploads.AnyAsync(x => x.Id == uploadId && x.CreatedBy == userId && x.UploadType == UploadTypeCodes.Users, ct)) throw new InvalidOperationException("Invalid user upload.");
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { partnerId, rows }, JsonOptions))));
    var previous = await db.UserUploadBatches.FindAsync([uploadId], ct);
    if (previous is not null) {
      if (previous.PartnerId != partnerId || previous.RequestHash != hash) throw new PartnerMemberRequestConflictException();
      return new(uploadId, previous.RequestedCount, []);
    }
    var emails = rows.Select(row => row.Email).ToArray();
    var active = await db.PartnerUsers.Where(x => x.PartnerId == partnerId && x.IsActive && emails.Contains(x.RefUser.Email)).Select(x => x.RefUser.Email).ToArrayAsync(ct);
    var pending = await db.PartnerMemberRequests.Where(x => x.PartnerId == partnerId && x.CodeDecodePartnerMemberRequestStatus == 1 && emails.Contains(x.Email)).Select(x => x.Email).ToArrayAsync(ct);
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var errors = new List<PartnerUserImportError>();
    foreach (var row in rows) {
      if (!seen.Add(row.Email)) errors.Add(new(row.Row, "Email address", "This email address is repeated in the file."));
      if (active.Contains(row.Email, StringComparer.OrdinalIgnoreCase)) errors.Add(new(row.Row, "Email address", "This person is already an active member of this partner."));
      if (pending.Contains(row.Email, StringComparer.OrdinalIgnoreCase)) errors.Add(new(row.Row, "Email address", "A user request for this person is already being processed."));
      if (!PartnerMemberRoles.Valid(workspace.BusinessCategory, row.Roles)) errors.Add(new(row.Row, "Permissions", "Select permissions supported by this partner, including the matching view permissions."));
    }
    if (errors.Count != 0) return new(uploadId, 0, errors);
    db.UserUploadBatches.Add(new UserUploadBatch { UploadId = uploadId, PartnerId = partnerId, RequestedCount = rows.Count, RequestHash = hash });
    foreach (var row in rows) {
      var request = CreateRequest(userId, new(Guid.NewGuid(), partnerId, row.FirstName, row.LastName, row.Email, row.Roles), workspace.BusinessCategory);
      db.UploadMemberRequests.Add(new UploadMemberRequest { UploadId = uploadId, Request = request });
    }
    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    if (changes is not null) await changes.PublishAsync(["users"], userId, partnerId, ct);
    return new(uploadId, rows.Count, []);
  }
  public async Task<PartnerMemberStatus?> StatusAsync(string userId, Guid id, CancellationToken cancellationToken)
  {
    var accessible = Accessible(userId).Select(x => x.Id);
    return await db.PartnerMemberRequests.Where(x => x.Id == id && accessible.Contains(x.PartnerId))
      .Select(x => new PartnerMemberStatus(x.Id, x.PartnerId, x.Email, x.Status, x.CreatedBy == userId && db.TemporaryPasswords.Any(p =>
        p.RequestId == x.Id && p.CanReveal && p.AvailableUntilUtc > DateTimeOffset.UtcNow))).SingleOrDefaultAsync(cancellationToken);
  }
  public async Task CompleteAsync(ConsumedRecord record, CancellationToken cancellationToken)
  {
    if (record.Payload is null || Encoding.UTF8.GetByteCount(record.Payload) > 16384) throw new InvalidDataException();
    var message = JsonSerializer.Deserialize<Provisioned>(record.Payload, JsonOptions);
    if (message is null || message.EventId == Guid.Empty || message.EventType != "partner.member.provisioned"
      || message.Source != "account" || message.SchemaVersion != 1 || message.OccurredAtUtc == default
      || record.Key != message.EventId.ToString() || message.User is not {} user
      || string.IsNullOrWhiteSpace(user.Id) || user.Id.Length > 255 || user.Id != user.Id.Trim()
      || user.Id.Any(char.IsControl) || string.IsNullOrWhiteSpace(user.FirstName) || user.FirstName.Length > 255
      || string.IsNullOrWhiteSpace(user.LastName) || user.LastName.Length > 255 || user.Email?.Length > 254
      || string.IsNullOrWhiteSpace(user.Provider) || user.Provider.Length > 100 || user.CreatedAtUtc == default
      || user.Role is not (RefUser.NormalRole or RefUser.AdministratorRole)) throw new InvalidDataException();
    db.ChangeTracker.Clear();
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {message.PartnerId}")
      .SingleOrDefaultAsync(cancellationToken);
    var request = await db.PartnerMemberRequests.FromSqlInterpolated(
      $"SELECT * FROM PartnerMemberRequests WITH (UPDLOCK, HOLDLOCK) WHERE Id = {message.EventId}").SingleAsync(cancellationToken);
    if (request.PartnerId != message.PartnerId || request.CreatedBy != message.CreatedBy || request.RequestHash != message.RequestHash
      || !string.Equals(request.Email, user.Email, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
    if (request.Status != "Pending") return;
    var temporaryPassword = await db.TemporaryPasswords.SingleOrDefaultAsync(x => x.RequestId == request.Id, cancellationToken);
    // A suspended workspace or revoked inviter must not acquire new memberships while Kafka was processing.
    if (!await Accessible(request.CreatedBy).AnyAsync(x => x.Id == request.PartnerId, cancellationToken))
      request.Status = "Cancelled";
    else
    {
      var reference = await db.RefUsers.FromSqlInterpolated(
        $"SELECT * FROM RefUsers WITH (UPDLOCK, HOLDLOCK) WHERE ExternalUserId = {user.Id}").SingleOrDefaultAsync(cancellationToken);
      if (reference is null)
      {
        reference = new RefUser { ExternalUserId = user.Id, FirstName = user.FirstName, LastName = user.LastName,
          Email = user.Email!, Provider = user.Provider, Role = user.Role, CreatedAtUtc = user.CreatedAtUtc };
        db.RefUsers.Add(reference);
      }
      var membership = await db.PartnerUsers.SingleOrDefaultAsync(x => x.PartnerId == request.PartnerId && x.RefUserId == reference.Id, cancellationToken);
      if (membership is null) db.PartnerUsers.Add(new PartnerUser { PartnerId = request.PartnerId, RefUserId = reference.Id });
      else membership.IsActive = true;
      request.RefUserId = reference.Id; request.Status = "Completed";
    }
    if (temporaryPassword is not null)
    {
      if (message.TemporaryPasswordAssigned && request.Status == "Completed") temporaryPassword.CanReveal = true;
      else db.TemporaryPasswords.Remove(temporaryPassword);
    }
    request.CompletedAtUtc = DateTimeOffset.UtcNow;
    var batchId = await db.UploadMemberRequests.Where(link => link.RequestId == request.Id).Select(link => (Guid?)link.UploadId).SingleOrDefaultAsync(cancellationToken);
    if (batchId.HasValue && request.Status == "Completed" && request.RefUserId.HasValue
      && !await db.UploadRecords.AnyAsync(link => link.UploadId == batchId.Value && link.RefUserId == request.RefUserId, cancellationToken))
      db.UploadRecords.Add(new UploadRecord { UploadId = batchId.Value, RefUserId = request.RefUserId });
    await db.SaveChangesAsync(cancellationToken);
    if (batchId.HasValue) {
      var batch = await db.UserUploadBatches.SingleAsync(x => x.UploadId == batchId.Value, cancellationToken);
      if (batch.FinishedAtUtc is null && !await db.UploadMemberRequests.AnyAsync(link => link.UploadId == batch.UploadId && link.Request.CodeDecodePartnerMemberRequestStatus == 1, cancellationToken)) {
        batch.FinishedAtUtc = DateTimeOffset.UtcNow;
        var count = await db.UploadRecords.CountAsync(link => link.UploadId == batch.UploadId && link.RefUserId != null, cancellationToken);
        if (count > 0) await PartnerCatalogActivityOutbox.QueueImportAsync(db, batch.PartnerId, request.CreatedBy, "users.imported", count, batch.UploadId, settings.Value.NotificationTopic, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
      }
    }
    await tx.CommitAsync(cancellationToken);
    if (changes is not null) await changes.PublishAsync(["users"], request.CreatedBy, request.PartnerId, cancellationToken);
  }
  private static PartnerMemberStatus Status(PartnerMemberRequest request) => new(request.Id, request.PartnerId, request.Email, request.Status);
  public async Task<IReadOnlyList<PartnerMemberStatus>> RecentAsync(string userId, CancellationToken cancellationToken)
  {
    var partnerIds = Accessible(userId).Select(x => x.Id);
    return await db.PartnerMemberRequests.AsNoTracking().Where(x => x.CreatedBy == userId && partnerIds.Contains(x.PartnerId))
      .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(50)
      .Select(x => new PartnerMemberStatus(x.Id, x.PartnerId, x.Email, x.Status, db.TemporaryPasswords.Any(p =>
        p.RequestId == x.Id && p.CanReveal && p.AvailableUntilUtc > DateTimeOffset.UtcNow))).ToArrayAsync(cancellationToken);
  }
  public async Task<PartnerTemporaryPassword?> TemporaryPasswordAsync(string userId, Guid requestId, CancellationToken cancellationToken)
  {
    var partnerIds = Accessible(userId).Select(x => x.Id);
    var temporary = await db.TemporaryPasswords.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == requestId
      && x.CanReveal && x.AvailableUntilUtc > DateTimeOffset.UtcNow && x.Request.Status == "Completed"
      && x.Request.CreatedBy == userId && partnerIds.Contains(x.Request.PartnerId), cancellationToken);
    return temporary is null ? null : new(passwords.Unprotect(requestId, temporary.ProtectedPassword), temporary.AvailableUntilUtc);
  }
  private sealed record Provisioned(Guid EventId, string EventType, int SchemaVersion, DateTimeOffset OccurredAtUtc,
    string Source, Guid PartnerId, string CreatedBy, string RequestHash, ProvisionedUser User, bool TemporaryPasswordAssigned);
  private sealed record ProvisionedUser(string Id, string FirstName, string LastName, string Email, string Provider, string Role, DateTimeOffset CreatedAtUtc);
}
