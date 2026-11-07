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
    select new PartnerMemberWorkspace(partner.Id, partner.DisplayName, application.BusinessCategory);
  public async Task<IReadOnlyList<PartnerMemberWorkspace>> WorkspacesAsync(string userId, CancellationToken cancellationToken) =>
    await Workspaces(userId).ToArrayAsync(cancellationToken);

  public async Task<PartnerMemberStatus?> QueueAsync(string userId, PartnerMemberCommand command, CancellationToken cancellationToken)
  {
    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    // Serialize requests for one workspace, including browser retries and concurrent invitations.
    await db.Partners.FromSqlInterpolated($"SELECT * FROM Partners WITH (UPDLOCK, HOLDLOCK) WHERE Id = {command.PartnerId}")
      .SingleOrDefaultAsync(cancellationToken);
    var workspace = await Workspaces(userId, command.PartnerId).SingleOrDefaultAsync(cancellationToken);
    if (workspace is null) return null;
    var existing = await db.PartnerMemberRequests.FindAsync([command.RequestId], cancellationToken);
    if (existing is not null)
    {
      if (existing.PartnerId != command.PartnerId || existing.CreatedBy != userId || existing.Email != command.Email
        || existing.FirstName != command.FirstName || existing.LastName != command.LastName) throw new PartnerMemberRequestConflictException();
      return Status(existing);
    }
    var pending = await db.PartnerMemberRequests.SingleOrDefaultAsync(x => x.PartnerId == command.PartnerId
      && x.Email == command.Email && x.Status == "Pending", cancellationToken);
    if (pending is not null) return Status(pending);
    var now = DateTimeOffset.UtcNow;
    var protectedPassword = passwords.Protect(command.RequestId, passwords.Generate());
    var payload = JsonSerializer.Serialize(new { eventId = command.RequestId, eventType = "partner.member.requested",
      schemaVersion = 1, occurredAtUtc = now, source = "ebyahe", createdBy = userId, partnerId = command.PartnerId,
      businessCategory = workspace.BusinessCategory, command.FirstName, command.LastName, command.Email,
      protectedPassword }, JsonOptions);
    var request = new PartnerMemberRequest { Id = command.RequestId, PartnerId = command.PartnerId, CreatedBy = userId,
      FirstName = command.FirstName, LastName = command.LastName, Email = command.Email, BusinessCategory = workspace.BusinessCategory,
      CreatedAtUtc = now, RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))) };
    db.PartnerMemberRequests.Add(request);
    db.TemporaryPasswords.Add(new TemporaryPassword { RequestId = request.Id, ProtectedPassword = protectedPassword,
      CreatedAtUtc = now, AvailableUntilUtc = now.AddDays(7) });
    var message = new OutboxMessage { Id = request.Id, UserId = userId, EventType = "partner.member.requested",
      Topic = settings.Value.PartnerMembersTopic, MessageKey = request.Id.ToString(), Payload = payload,
      CreatedDateTime = now.UtcDateTime, NextAttemptDateTime = now.UtcDateTime };
    message.History.Add(Outgoing.Record(message, "Pending", now.UtcDateTime)); db.OutboxMessages.Add(message);
    await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
    if (changes is not null) await changes.PublishAsync(["users"], userId, command.PartnerId, cancellationToken);
    return Status(request);
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
    await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
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
