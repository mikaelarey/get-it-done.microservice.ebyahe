using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using EByahe.Application.Authorization;
using EByahe.Domain.Repositories;
using EByahe.Infrastructure.Persistence;

namespace EByahe.Application.Realtime;

[Authorize]
public sealed class RecordsHub : Hub
{
  public const string Reviewers = "application-reviewers", BookingReaders = "booking-readers";
  public override async Task OnConnectedAsync()
  {
    if (PartnerApplicationReviewAuthorization.HasAccess(Context.User!))
      await Groups.AddToGroupAsync(Context.ConnectionId, Reviewers);
    if (BookingReadAuthorization.HasAccess(Context.User!))
      await Groups.AddToGroupAsync(Context.ConnectionId, BookingReaders);
    await base.OnConnectedAsync();
  }
}

public sealed class SubjectUserIdProvider : IUserIdProvider
{
  public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

public sealed class RecordChangePublisher(EByaheDbContext db, IHubContext<RecordsHub> hub,
  ILogger<RecordChangePublisher> logger) : IRecordChangePublisher
{
  public async Task PublishAsync(string[] resources, string? userId = null, Guid? partnerId = null, CancellationToken ct = default)
  {
    try
    {
      // Resolve current membership at delivery, never trust client-supplied partner groups.
      var subjects = partnerId is null ? new List<string>() : await db.PartnerUsers.AsNoTracking()
        .Where(m => m.PartnerId == partnerId && m.IsActive && m.Partner.Status == "Approved")
        .Select(m => m.RefUser.ExternalUserId).Distinct().ToListAsync(ct);
      if (!string.IsNullOrWhiteSpace(userId)) subjects.Add(userId);
      var change = new { resources };
      if (subjects.Count > 0)
        await hub.Clients.Users(subjects.Distinct(StringComparer.Ordinal).ToArray()).SendAsync("RecordsChanged", change, ct);
      if (resources.Contains("bookings"))
        await hub.Clients.Group(RecordsHub.BookingReaders).SendAsync("RecordsChanged", change, ct);
      // Reviewers see partner summary tiles as well as the application directory.
      await hub.Clients.Group(RecordsHub.Reviewers).SendAsync("RecordsChanged", change, ct);
    }
    catch (Exception ex) { logger.LogWarning(ex, "Live record delivery failed; committed records remain available."); }
  }
}
