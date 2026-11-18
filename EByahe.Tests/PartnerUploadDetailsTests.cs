using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task UploadDetailsSelectsOnlyLinkedRecordsForEachOfTheFourTypesAndPagesTheBatch()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var route = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    var trip = new ScheduledTrip { PartnerId = partner.Id, BusId = bus.Id, BusRoute = route,
      DepartureAtUtc = DateTimeOffset.UtcNow.AddDays(3), BaseFare = 500, AvailableSeats = 40 };
    db.ScheduledTrips.Add(trip);
    var actor = await db.RefUsers.SingleAsync(user => user.ExternalUserId == PartnerApiFactory.Subject);
    var uploads = Enumerable.Range(1, 4).Select(code => new Upload { UploadType = code, CreatedBy = PartnerApiFactory.Subject }).ToArray();
    db.Uploads.AddRange(uploads); await db.SaveChangesAsync();
    db.UploadRecords.AddRange(new UploadRecord { UploadId = uploads[0].Id, BusId = bus.Id },
      new UploadRecord { UploadId = uploads[1].Id, TerminalId = origin.Id },
      new UploadRecord { UploadId = uploads[2].Id, ScheduledTripId = trip.Id },
      new UploadRecord { UploadId = uploads[3].Id, RefUserId = actor.Id });
    // A second bus in the same partner must not appear unless linked to this upload.
    db.Buses.Add(new Bus { PartnerId = partner.Id, FleetNumber = "UNLINKED", PlateNumber = "UPL-0002", CodeDecodeBusClass = 2, SeatCapacity = 40 });
    await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var roles = new[] { "bus.read", "terminal.read", "scheduled-trip.read", "member.manage" };
    foreach (var (upload, index) in uploads.Select((upload, index) => (upload, index)))
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles(roles[index])));
      using var response = await client.GetAsync($"/api/partner/uploads/{upload.Id}?page=1&pageSize=1");
      Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
      var details = (await response.Content.ReadFromJsonAsync<UploadDetails>())!;
      Assert.Equal(upload.Id, details.Id); Assert.Equal(index + 1, details.UploadType); Assert.Equal(1, details.TotalCount);
      Assert.Equal((actor.FirstName + " " + actor.LastName).Trim(), details.CreatedByName);
      switch (index)
      {
        case 0: Assert.Equal(bus.Id, Assert.Single(details.Buses!).Id); Assert.Null(details.Terminals); break;
        case 1: Assert.Equal(origin.Id, Assert.Single(details.Terminals!).Id); Assert.Null(details.Buses); break;
        case 2: Assert.Equal(trip.Id, Assert.Single(details.ScheduledTrips!).Id); Assert.Equal(500, details.ScheduledTrips![0].Fare); break;
        case 3:
          Assert.Equal(actor.Id, Assert.Single(details.Users!).Id);
          using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
            Assert.Equal(new[] { "createdAtUtc", "email", "id", "name", "provider" },
              json.RootElement.GetProperty("users")[0].EnumerateObject().Select(property => property.Name).Order());
          break;
      }
      var next = (await client.GetFromJsonAsync<UploadDetails>($"/api/partner/uploads/{upload.Id}?page=2&pageSize=1"))!;
      Assert.Equal(1, next.TotalCount); Assert.Equal(2, next.Page);
      Assert.Empty(next.Buses ?? []); Assert.Empty(next.Terminals ?? []); Assert.Empty(next.ScheduledTrips ?? []); Assert.Empty(next.Users ?? []);
      // A different resource's read permission does not grant access to this upload.
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles(roles[(index + 1) % 4])));
      Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/uploads/{upload.Id}")).StatusCode);
    }
    // Stable paging across more than one linked row, with current business values.
    var second = await db.Buses.SingleAsync(row => row.FleetNumber == "UNLINKED");
    db.UploadRecords.Add(new UploadRecord { UploadId = uploads[0].Id, BusId = second.Id });
    var first = await db.Buses.SingleAsync(row => row.Id == bus.Id); first.FleetNumber = "AAA-UPDATED"; await db.SaveChangesAsync();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read")));
    var firstPage = (await client.GetFromJsonAsync<UploadDetails>($"/api/partner/uploads/{uploads[0].Id}?pageSize=1"))!;
    var secondPage = (await client.GetFromJsonAsync<UploadDetails>($"/api/partner/uploads/{uploads[0].Id}?page=2&pageSize=1"))!;
    Assert.Equal(2, firstPage.TotalCount); Assert.Equal("AAA-UPDATED", Assert.Single(firstPage.Buses!).FleetNumber);
    Assert.Equal(second.Id, Assert.Single(secondPage.Buses!).Id);
  }

  [SqlServerFact]
  public async Task UploadDetailsRejectsForeignAndMixedWorkspacesAndRevokedMembershipWithoutLeakingMetadata()
  {
    var (partner, bus, _, _) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var foreign = new Partner { Code = "UPLOAD-FOREIGN", DisplayName = "Foreign upload partner" };
    var foreignBus = new Bus { Partner = foreign, FleetNumber = "FOREIGN", PlateNumber = "UPL-FOREIGN", CodeDecodeBusClass = 2, SeatCapacity = 40 };
    var foreignUpload = new Upload { CreatedBy = PartnerApiFactory.Subject, UploadType = 1 };
    var mixed = new Upload { CreatedBy = PartnerApiFactory.Subject, UploadType = 1 };
    var local = new Upload { CreatedBy = PartnerApiFactory.Subject, UploadType = 1 };
    db.AddRange(foreignBus, foreignUpload, mixed, local); await db.SaveChangesAsync();
    db.UploadRecords.AddRange(new UploadRecord { UploadId = foreignUpload.Id, BusId = foreignBus.Id },
      new UploadRecord { UploadId = mixed.Id, BusId = bus.Id }, new UploadRecord { UploadId = mixed.Id, BusId = foreignBus.Id },
      new UploadRecord { UploadId = local.Id, BusId = bus.Id }); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read")));
    foreach (var id in new[] { foreignUpload.Id, mixed.Id, Guid.NewGuid() })
    {
      using var response = await client.GetAsync($"/api/partner/uploads/{id}"); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      Assert.DoesNotContain("Foreign upload", await response.Content.ReadAsStringAsync());
    }
    Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/partner/uploads/{local.Id}")).StatusCode);
    partner.Status = "Suspended"; db.Partners.Update(partner); await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/uploads/{local.Id}")).StatusCode);
    partner.Status = "Approved"; await db.SaveChangesAsync();
    var membership = await db.PartnerUsers.SingleAsync(member => member.PartnerId == partner.Id); membership.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/uploads/{local.Id}")).StatusCode);
  }

  [SqlServerFact]
  public async Task UploadDetailsRequiresAReadRoleValidPagingAndAuthenticationAndLimitsEmptyUploadsToTheirCreator()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var colleague = MembershipUser("upload-colleague"); db.PartnerUsers.Add(new PartnerUser { PartnerId = partner.Id, RefUser = colleague });
    var upload = new Upload { UploadType = 2, CreatedBy = PartnerApiFactory.Subject }; db.Uploads.Add(upload); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var path = $"/api/partner/uploads/{upload.Id}";
    client.DefaultRequestHeaders.Authorization = null; Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.write")));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read")));
    var details = (await client.GetFromJsonAsync<UploadDetails>(path))!; Assert.Equal(0, details.TotalCount); Assert.Empty(details.Terminals!);
    foreach (var query in new[] { "?page=0", "?page=1000001", "?pageSize=0", "?pageSize=51" })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + query)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/partner/uploads/not-a-guid")).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: colleague.ExternalUserId, resourceAccess: BusRoles("terminal.read")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
  }
}
