using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Application.Dtos.Responses;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;
public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ApprovedPartnerOwnershipChangesPublishDirectorySnapshotsInTheSameTransaction()
  {
    await using var db = CreateDatabase();
    var partner = new EByahe.Domain.Models.Partner { Code = "OWNER-TEST", DisplayName = "Owned partner",
      OwnerUserId = "owner-subject", Status = "Suspended" };
    db.Partners.Add(partner); await db.SaveChangesAsync();
    partner.Status = "Approved"; await db.SaveChangesAsync();
    partner.OwnerUserId = null; await db.SaveChangesAsync();
    var events = await db.OutboxMessages.OrderBy(x => x.CreatedDateTime).ToArrayAsync();
    Assert.Equal(3, events.Length);
    var snapshots = events.Select(x => JsonDocument.Parse(x.Payload)).ToArray();
    try
    {
      Assert.Equal("partner.created", events[0].EventType);
      Assert.Equal("partner.updated", events[1].EventType);
      Assert.False(snapshots[0].RootElement.GetProperty("partner").GetProperty("isApproved").GetBoolean());
      Assert.True(snapshots[1].RootElement.GetProperty("partner").GetProperty("isApproved").GetBoolean());
      Assert.Equal(JsonValueKind.Null, snapshots[2].RootElement.GetProperty("partner").GetProperty("ownerExternalUserId").ValueKind);
      Assert.All(snapshots, s => Assert.Equal(partner.Id, s.RootElement.GetProperty("partner").GetProperty("id").GetGuid()));
      Assert.All(events, e => Assert.Equal("system:ebyahe", e.UserId));
    }
    finally { foreach (var snapshot in snapshots) snapshot.Dispose(); }
    var before = events.Length;
    partner.DisplayName = "Renamed"; await db.SaveChangesAsync();
    Assert.Equal(before, await db.OutboxMessages.CountAsync());
  }

  [SqlServerFact]
  public async Task BookingReplayQueuesOneCompactActivityAndPartnerSubmissionQueuesAnother()
  {
    await SeedBookingTripAsync(); using var factory=new PartnerApiFactory(ConnectionString); using var client=factory.AuthenticatedClient();
    var request=BookingRequest();
    using var first=await client.PostAsJsonAsync("/api/bus-bookings",request); Assert.Equal(HttpStatusCode.Created,first.StatusCode);
    var receipt=(await first.Content.ReadFromJsonAsync<BusBookingResponse>())!;
    using var replay=await client.PostAsJsonAsync("/api/bus-bookings",request); Assert.Equal(HttpStatusCode.OK,replay.StatusCode);
    using var form=ApplicationForm(); using var submitted=await client.PostAsync("/api/partner-applications",form); Assert.Equal(HttpStatusCode.Created,submitted.StatusCode);
    var application=(await submitted.Content.ReadFromJsonAsync<PartnerApplicationResponse>())!;
    await using var db=CreateDatabase(); var events=await db.OutboxMessages.ToArrayAsync(); Assert.Equal(2,events.Length);
    var booking=Assert.Single(events,x=>x.EventType=="booking.created"); Assert.Equal(receipt.Id,booking.Id);
    var partner=Assert.Single(events,x=>x.EventType=="partner-application.submitted"); Assert.Equal(application.Id,partner.Id);
    Assert.All(events,message=>{
      Assert.Equal("Pending",message.Status); Assert.Equal("get-it-done.notifications.v1",message.Topic); Assert.Equal(PartnerApiFactory.Subject,message.MessageKey);
      using var json=JsonDocument.Parse(message.Payload); var root=json.RootElement;
      Assert.Equal(message.Id,root.GetProperty("eventId").GetGuid()); Assert.Equal("ebyahe",root.GetProperty("source").GetString());
      Assert.Equal(PartnerApiFactory.Subject,root.GetProperty("createdBy").GetString()); Assert.Equal(1,root.GetProperty("schemaVersion").GetInt32());
      Assert.True(System.Text.Encoding.UTF8.GetByteCount(message.Payload)<2048);
      foreach(var forbidden in new[]{"accountNumber","storageKey","payment","contact","passengers","Test Traveler"}) Assert.DoesNotContain(forbidden,message.Payload);
    });
    using var b=JsonDocument.Parse(booking.Payload); Assert.Equal("/booking-details?reference="+receipt.Reference,b.RootElement.GetProperty("link").GetString());
    Assert.Equal(1,b.RootElement.GetProperty("details").GetProperty("reservedSeats").GetInt32());
    using var p=JsonDocument.Parse(partner.Payload); Assert.Equal("/partner-application-details/"+application.Id,p.RootElement.GetProperty("link").GetString());
    Assert.Equal(2,await db.Outgoing.CountAsync());
  }

  [SqlServerFact]
  public async Task FailedBookingSaveDoesNotPersistOrLeaveTrackedActivityEvents()
  {
    await SeedBookingTripAsync(); await using var db=CreateDatabase();
    var trip=(await new EByahe.Infrastructure.Repositories.BusCatalogRepository(db,TimeProvider.System).GetAsync(TestTripId,default))!;
    var booking=new EByahe.Domain.Models.BusBooking {Id=Guid.NewGuid(),RequestId=Guid.NewGuid(),ScheduledTripId=trip.Id,CreatedBy="test-owner",
      Reference="BK-OUTBOX-ROLLBACK",CreatedAtUtc=DateTimeOffset.UtcNow,RequestHash=new string('A',64),DetailsJson="{}"};
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectActivity ON dbo.OutboxMessages AFTER INSERT AS THROW 51010,'Test failure',1;");
    await Assert.ThrowsAsync<DbUpdateException>(()=>new EByahe.Infrastructure.Repositories.BusBookingRepository(db,TimeProvider.System).ReserveAndAddAsync(booking,trip,1,default));
    Assert.DoesNotContain(db.ChangeTracker.Entries(),x=>x.Entity is EByahe.Domain.Models.OutboxMessage or EByahe.Domain.Models.Outgoing);
    await using var verify=CreateDatabase(); Assert.Empty(await verify.OutboxMessages.ToArrayAsync()); Assert.Empty(await verify.BusBookings.ToArrayAsync());
    Assert.Equal(40,(await verify.ScheduledTrips.SingleAsync()).AvailableSeats);
  }
}
