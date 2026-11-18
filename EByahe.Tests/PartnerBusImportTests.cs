using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static MultipartFormDataContent BusUpload(Guid partnerId, params string[][] rows)
  {
    var content = new MultipartFormDataContent(); content.Add(new StringContent(partnerId.ToString()), "partnerId");
    var file = new ByteArrayContent(BusExcelImportTests.Workbook([BusExcelImportTests.Headers, .. rows]));
    file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    content.Add(file, "file", "buses.xlsx"); return content;
  }
  [SqlServerFact]
  public async Task PartnerBusImportIsAtomicReportsRowsAndQueuesOneSummaryNotification()
  {
    var partner = await SeedMemberPartner();
    await using var db = CreateDatabase(); var initialEvents = await db.OutboxMessages.CountAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    using var file = BusUpload(partner.Id, [" bus-101 ", "abc-1234", " Deluxe ", "40"], ["BUS-102", "XYZ 1234", "Luxury", "50"]);
    using var response = await client.PostAsync("/api/partner/buses/import", file);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    var result = (await response.Content.ReadFromJsonAsync<PartnerBusImportResult>())!;
    Assert.Equal(2, result.ImportedCount); Assert.Empty(result.Errors);
    var buses = await db.Buses.OrderBy(bus => bus.FleetNumber).ToArrayAsync(); Assert.Equal(2, buses.Length);
    Assert.All(buses, bus => { Assert.Equal(partner.Id, bus.PartnerId); Assert.True(bus.IsActive); });
    Assert.Equal("BUS-101", buses[0].FleetNumber); Assert.Equal("ABC-1234", buses[0].PlateNumber); Assert.Equal(40, buses[0].SeatCapacity);
    var upload = Assert.Single(await db.Uploads.ToArrayAsync());
    Assert.Equal(UploadTypeCodes.Buses, upload.UploadType); Assert.Equal(PartnerApiFactory.Subject, upload.CreatedBy);
    Assert.Equal(2, await db.UploadRecords.CountAsync(row => row.UploadId == upload.Id && row.BusId != null));
    var logs = await db.BusLogs.ToArrayAsync(); Assert.Equal(2, logs.Length);
    Assert.All(logs, log => { Assert.Equal("Imported", log.Action); Assert.Null(log.PreviousValue); Assert.Equal(upload.Id, log.UploadId); Assert.Equal(PartnerApiFactory.Subject, log.CreatedBy); });
    Assert.Equal(initialEvents + 1, await db.OutboxMessages.CountAsync());
    var summary = Assert.Single(await db.OutboxMessages.Where(row => row.EventType == "buses.imported").ToArrayAsync());
    using var payload = JsonDocument.Parse(summary.Payload);
    Assert.Equal("Successfully created 2 buses using file upload.", payload.RootElement.GetProperty("message").GetString());
    Assert.Equal(2, payload.RootElement.GetProperty("details").GetProperty("recordCount").GetInt32());
    Assert.Equal(upload.Id, payload.RootElement.GetProperty("details").GetProperty("uploadId").GetGuid());
    Assert.Empty(await db.OutboxMessages.Where(row => row.EventType == "bus.created").ToArrayAsync());
    using var duplicate = BusUpload(partner.Id, ["BUS-103", "NEW-1003", "Deluxe", "40"], ["BUS-101", "ABC-1234", "Luxury", "50"]);
    using var conflict = await client.PostAsync("/api/partner/buses/import", duplicate);
    Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    var rejected = (await conflict.Content.ReadFromJsonAsync<PartnerBusImportResult>())!;
    Assert.Equal(0, rejected.ImportedCount); Assert.Contains(rejected.Errors, error => error.Row == 3 && error.Field == "Fleet number");
    Assert.Contains(rejected.Errors, error => error.Row == 3 && error.Field == "Plate number");
    using var invalid = BusUpload(partner.Id, ["BUS-103", "NEW-1003", "Deluxe", "40"], ["BUS-104", "NEW-1004", "Deluxe", "0"]);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/partner/buses/import", invalid)).StatusCode);
    Assert.Equal(2, await db.Buses.CountAsync()); Assert.Equal(initialEvents + 1, await db.OutboxMessages.CountAsync());
    Assert.Equal(3, await db.Uploads.CountAsync()); Assert.Equal(2, await db.UploadRecords.CountAsync()); Assert.Equal(2, await db.BusLogs.CountAsync());
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectBusImport ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted WHERE EventType='buses.imported') THROW 51062,'Test outbox failure',1;");
    using var failed = BusUpload(partner.Id, ["BUS-103", "NEW-1003", "Deluxe", "40"]);
    Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync("/api/partner/buses/import", failed)).StatusCode);
    Assert.Equal(4, await db.Uploads.CountAsync()); Assert.Equal(2, await db.UploadRecords.CountAsync()); Assert.Equal(2, await db.BusLogs.CountAsync());
    Assert.Equal(2, await db.Buses.CountAsync()); Assert.Equal(initialEvents + 1, await db.OutboxMessages.CountAsync());
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectBusImport");
    using var retry = BusUpload(partner.Id, ["BUS-103", "NEW-1003", "Deluxe", "40"]);
    Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/partner/buses/import", retry)).StatusCode);
    Assert.Equal(3, await db.Buses.CountAsync()); Assert.Equal(initialEvents + 2, await db.OutboxMessages.CountAsync());
    Assert.Equal(5, await db.Uploads.CountAsync()); Assert.Equal(3, await db.UploadRecords.CountAsync()); Assert.Equal(3, await db.BusLogs.CountAsync());
    var messages = await db.OutboxMessages.Where(row => row.EventType == "buses.imported").Select(row => row.Payload).ToArrayAsync();
    Assert.Contains(messages, message => { using var json = JsonDocument.Parse(message); return json.RootElement.GetProperty("message").GetString() == "Successfully created 1 bus using file upload."; });
  }
  [SqlServerFact]
  public async Task PartnerBusImportRequiresWritePermissionAndAnActiveApprovedBusOperatorMembership()
  {
    var partner = await SeedMemberPartner();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    foreach (var roles in new object?[] { null, BusRoles("bus.read"), BusRoles("bus.write"), BookingRoles() })
    {
      client.DefaultRequestHeaders.Authorization = roles is null ? null : new("Bearer", factory.Token(resourceAccess: roles));
      using var file = BusUpload(partner.Id, ["BUS-101", "ABC-1234", "Deluxe", "40"]);
      Assert.Equal(roles is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden,
        (await client.PostAsync("/api/partner/buses/import", file)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    using var foreignFile = BusUpload(Guid.NewGuid(), ["BUS-101", "ABC-1234", "Deluxe", "40"]);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/buses/import", foreignFile)).StatusCode);
    await using var db = CreateDatabase(); var membership = await db.PartnerUsers.SingleAsync();
    membership.IsActive = false; await db.SaveChangesAsync();
    using var inactiveFile = BusUpload(partner.Id, ["BUS-101", "ABC-1234", "Deluxe", "40"]);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/buses/import", inactiveFile)).StatusCode);
    membership.IsActive = true; var savedPartner = await db.Partners.FindAsync(partner.Id); savedPartner!.Status = "Suspended"; await db.SaveChangesAsync();
    using var suspendedFile = BusUpload(partner.Id, ["BUS-101", "ABC-1234", "Deluxe", "40"]);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/buses/import", suspendedFile)).StatusCode);
    Assert.Empty(await db.Buses.ToArrayAsync()); Assert.Empty(await db.OutboxMessages.Where(row => row.EventType == "bus.created" || row.EventType == "buses.imported").ToArrayAsync());
    Assert.Empty(await db.Uploads.ToArrayAsync()); Assert.Empty(await db.UploadRecords.ToArrayAsync()); Assert.Empty(await db.BusLogs.ToArrayAsync());
  }
}
