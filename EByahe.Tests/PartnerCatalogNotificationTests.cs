using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static readonly string[] CatalogTypes = ["bus.created", "terminal.created", "scheduled-trip.created"];

  [SqlServerFact]
  public async Task CatalogCreationQueuesOnlyActiveUsersOfTheSelectedPartnerAndNotOtherWorkspacesOrAdministrators()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var owner = await db.RefUsers.SingleAsync(user => user.ExternalUserId == PartnerApiFactory.Subject);
    var colleague = MembershipUser("partner-colleague"); var inactive = MembershipUser("inactive-colleague"); var foreignUser = MembershipUser("foreign-partner-user");
    var foreign = new Partner { Code = "NOTIFICATION-FOREIGN", DisplayName = "Foreign partner" };
    db.PartnerUsers.AddRange(new PartnerUser { PartnerId = partner.Id, RefUser = colleague },
      new PartnerUser { PartnerId = partner.Id, RefUser = inactive, IsActive = false },
      new PartnerUser { Partner = foreign, RefUser = foreignUser }, new PartnerUser { Partner = foreign, RefUserId = owner.Id });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var busCommand = new { partnerId = partner.Id, fleetNumber = "NOTIFY-BUS", plateNumber = "NTF-0001", busClass = "Deluxe", seatCapacity = 40 };
    var terminalCommand = new { partnerId = partner.Id, locationId = origin.LocationId, name = "Notification terminal", address = "Private terminal address" };
    var tripCommand = new { partnerId = partner.Id, busId = bus.Id, originTerminalId = origin.Id, destinationTerminalId = destination.Id,
      departureAtUtc = DateTimeOffset.UtcNow.AddDays(3), durationMinutes = 120, baseFare = 550m };
    foreach (var (path, command) in new (string, object)[] { ("buses", busCommand), ("terminals", terminalCommand), ("scheduled-trips", tripCommand) })
    {
      using var created = await client.PostAsJsonAsync("/api/partner/" + path, command); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
      using var conflict = await client.PostAsJsonAsync("/api/partner/" + path, command); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
    var events = await db.OutboxMessages.Where(message => CatalogTypes.Contains(message.EventType)).ToArrayAsync(); Assert.Equal(3, events.Length);
    foreach (var message in events)
    {
      Assert.Equal("get-it-done.notifications.v1", message.Topic); Assert.Equal("Pending", message.Status);
      Assert.Equal(PartnerApiFactory.Subject, message.MessageKey); Assert.Equal(PartnerApiFactory.Subject, message.UserId);
      using var json = JsonDocument.Parse(message.Payload); var root = json.RootElement;
      Assert.Equal(partner.Id, root.GetProperty("details").GetProperty("partnerId").GetGuid());
      Assert.NotEqual(Guid.Empty, root.GetProperty("details").GetProperty("id").GetGuid());
      var targets = root.GetProperty("audiences").EnumerateArray().ToArray();
      Assert.All(targets, target => Assert.Equal("User", target.GetProperty("kind").GetString()));
      Assert.Equal(new[] { PartnerApiFactory.Subject, "partner-colleague" }.Order(), targets.Select(target => target.GetProperty("externalUserId").GetString()).Order());
      Assert.Contains(root.GetProperty("link").GetString(), new[] { "/buses", "/terminals", "/scheduled-trips" });
      Assert.DoesNotContain("Private terminal address", message.Payload); Assert.DoesNotContain("Administrators", message.Payload);
      Assert.True(Encoding.UTF8.GetByteCount(message.Payload) < 4096);
      Assert.Single(await db.Outgoing.Where(log => log.MessageId == message.Id && log.Status == "Pending").ToArrayAsync());
    }
  }

  [SqlServerFact]
  public async Task CatalogOutboxFailuresRollBackTheBusTerminalTripAndNewRouteThenAllowRetry()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    var commands = new (string Path, object Body)[] {
      ("buses", new { partnerId = partner.Id, fleetNumber = "ATOMIC-BUS", plateNumber = "ATM-0001", busClass = "Deluxe", seatCapacity = 40 }),
      ("terminals", new { partnerId = partner.Id, locationId = origin.LocationId, name = "Atomic terminal", address = "Terminal street" }),
      ("scheduled-trips", new { partnerId = partner.Id, busId = bus.Id, originTerminalId = origin.Id, destinationTerminalId = destination.Id,
        departureAtUtc = DateTimeOffset.UtcNow.AddDays(3), durationMinutes = 120, baseFare = 550m }) };
    var counts = (await db.Buses.CountAsync(), await db.BusTerminals.CountAsync(), await db.ScheduledTrips.CountAsync(), await db.BusRoutes.CountAsync());
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectCatalogActivity ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted WHERE EventType IN ('bus.created','terminal.created','scheduled-trip.created')) THROW 51060,'Test outbox failure',1;");
    foreach (var command in commands)
    {
      using var failed = await client.PostAsJsonAsync("/api/partner/" + command.Path, command.Body); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
      Assert.Equal(counts, (await db.Buses.CountAsync(), await db.BusTerminals.CountAsync(), await db.ScheduledTrips.CountAsync(), await db.BusRoutes.CountAsync()));
      Assert.Empty(await db.OutboxMessages.Where(message => CatalogTypes.Contains(message.EventType)).ToArrayAsync());
    }
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectCatalogActivity");
    foreach (var command in commands) Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/" + command.Path, command.Body)).StatusCode);
    Assert.Equal(3, await db.OutboxMessages.CountAsync(message => CatalogTypes.Contains(message.EventType)));
  }

  [SqlServerFact]
  public async Task LargePartnerAudiencesAreSplitIntoDisjointPayloadsWithinTheConsumerLimit()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    for (var index = 0; index < 26; index++)
    {
      var user = MembershipUser(new string('界', 250) + index.ToString("D3")); user.Email = $"batch-{index}@example.com";
      db.PartnerUsers.Add(new PartnerUser { PartnerId = partner.Id, RefUser = user });
    }
    await db.SaveChangesAsync(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/buses", new { partnerId = partner.Id, fleetNumber = "BATCH", plateNumber = "BAT-0001", busClass = "Deluxe", seatCapacity = 40 })).StatusCode);
    var events = await db.OutboxMessages.Where(message => message.EventType == "bus.created").ToArrayAsync(); Assert.Equal(2, events.Length);
    var recipients = new List<string>();
    foreach (var message in events)
    {
      Assert.True(Encoding.UTF8.GetByteCount(message.Payload) <= 65536); using var json = JsonDocument.Parse(message.Payload);
      recipients.AddRange(json.RootElement.GetProperty("audiences").EnumerateArray().Select(target => target.GetProperty("externalUserId").GetString()!));
    }
    Assert.Equal(27, recipients.Count); Assert.Equal(27, recipients.Distinct(StringComparer.Ordinal).Count());
  }
}
