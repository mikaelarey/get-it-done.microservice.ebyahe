using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static MultipartFormDataContent TripUpload(Guid partnerId, params (string Name, string[][] Rows)[] sheets)
  {
    var form = new MultipartFormDataContent(); form.Add(new StringContent(partnerId.ToString()), "partnerId");
    form.Add(new ByteArrayContent(ScheduledTripExcelImportTests.Workbook(sheets)), "file", "trips.xlsx"); return form;
  }

  [SqlServerFact]
  public async Task ScheduledTripImportCreatesDateSheetsRoutesInventoryAuditLinksAndOneNotificationAtomically()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    db.BusRoutes.Add(new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 120 }); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(3).Date); var first = date.ToString("yyyy-MM-dd"); var second = date.AddDays(1).ToString("yyyy-MM-dd");
    using var template = await client.GetAsync($"/api/partner/scheduled-trips/template?partnerId={partner.Id}&startDate={first}&endDate={second}");
    Assert.Equal(HttpStatusCode.OK, template.StatusCode); Assert.True(template.Headers.CacheControl!.NoStore);
    Assert.Equal("Scheduled-trip-import-template.xlsx", template.Content.Headers.ContentDisposition!.FileNameStar);
    var values = ScheduledTripExcelImportTests.Values(bus.Id, origin.Id, destination.Id);
    // Import the friendly labels from the actual downloaded template, preserving its hidden mappings.
    using var workbookStream = new MemoryStream(); workbookStream.Write(await template.Content.ReadAsByteArrayAsync()); workbookStream.Position = 0;
    using (var workbook = SpreadsheetDocument.Open(workbookStream, true))
    {
      var part = workbook.WorkbookPart!; var sheets = part.Workbook!.Sheets!.Elements<Sheet>().ToArray();
      var lists = (WorksheetPart)part.GetPartById(sheets.Single(sheet => sheet.Name == "_Lists").Id!.Value!);
      var choices = lists.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).ToArray();
      string Label(Guid id) => choices.Single(row => row.Elements<Cell>().Any(cell => cell.InnerText == id.ToString())).Elements<Cell>().First().InnerText;
      var busLabel = choices.Single(row => row.Elements<Cell>().ElementAt(4).InnerText == bus.Id.ToString()).Elements<Cell>().ElementAt(1).InnerText;
      string[] friendly = [Label(origin.Id), Label(destination.Id), busLabel, values[3], values[4], values[5]];
      foreach (var sheet in sheets.Where(sheet => sheet.Name == first || sheet.Name == second))
      {
        var worksheet = (WorksheetPart)part.GetPartById(sheet.Id!.Value!); var index = 0;
        foreach (var cell in worksheet.Worksheet!.GetFirstChild<SheetData>()!.Elements<Row>().ElementAt(1).Elements<Cell>()) {
          if (index != 4) ScheduledTripExcelImportTests.SetValue(cell, friendly[index]);
          index++;
        }
        worksheet.Worksheet!.Save();
      }
    }
    using var upload = new MultipartFormDataContent(); upload.Add(new StringContent(partner.Id.ToString()), "partnerId");
    upload.Add(new ByteArrayContent(workbookStream.ToArray()), "file", "trips.xlsx");
    using var response = await client.PostAsync("/api/partner/scheduled-trips/import", upload); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var result = (await response.Content.ReadFromJsonAsync<PartnerScheduledTripImportResult>())!; Assert.Equal(2, result.ImportedCount); Assert.Empty(result.Errors);
    var record = Assert.Single(await db.Uploads.ToArrayAsync()); Assert.Equal(UploadTypeCodes.ScheduledTrips, record.UploadType); Assert.Equal(record.Id, result.UploadId);
    var trips = await db.ScheduledTrips.OrderBy(trip => trip.DepartureAtUtc).ToArrayAsync(); Assert.Equal(2, trips.Length);
    Assert.All(trips, trip => { Assert.Equal(40, trip.AvailableSeats); Assert.Equal(550.25m, trip.BaseFare); Assert.Equal("Scheduled", trip.Status); Assert.Equal(TimeSpan.Zero, trip.DepartureAtUtc.Offset); });
    Assert.Equal(date.ToDateTime(new TimeOnly(7, 30)).AddHours(-8), trips[0].DepartureAtUtc.UtcDateTime);
    Assert.Single(await db.BusRoutes.ToArrayAsync()); Assert.Equal(2, await db.UploadRecords.CountAsync(link => link.UploadId == record.Id && link.ScheduledTripId != null));
    var logs = await db.ScheduledTripLogs.ToArrayAsync(); Assert.Equal(2, logs.Length);
    Assert.All(logs, log => { Assert.Equal(record.Id, log.UploadId); Assert.Equal("Imported", log.Action); Assert.Equal(PartnerApiFactory.Subject, log.CreatedBy); });
    var summary = Assert.Single(await db.OutboxMessages.Where(message => message.EventType == "scheduled-trips.imported").ToArrayAsync());
    using var payload = JsonDocument.Parse(summary.Payload);
    Assert.Equal("Successfully created 2 scheduled trips using file upload.", payload.RootElement.GetProperty("message").GetString());
    Assert.Equal("/uploads/" + record.Id, payload.RootElement.GetProperty("link").GetString());
    Assert.Empty(await db.OutboxMessages.Where(message => message.EventType == "scheduled-trip.created").ToArrayAsync());
    var details = (await client.GetFromJsonAsync<UploadDetails>("/api/partner/uploads/" + record.Id))!; Assert.Equal(2, details.TotalCount); Assert.Equal(2, details.ScheduledTrips!.Count);
    // Fail the final outbox insertion: no trip, route, audit or record link from that file can remain.
    var later = date.AddDays(3).ToString("yyyy-MM-dd");
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTripImport ON OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.CodeDecodeEventTypes et ON et.Code=i.CodeDecodeEventType WHERE et.Decode='scheduled-trips.imported') THROW 51077, 'Test trip import failure', 1;");
    using var failed = TripUpload(partner.Id, (later, [ScheduledTripExcelImportTests.Values(bus.Id, destination.Id, origin.Id)]));
    Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync("/api/partner/scheduled-trips/import", failed)).StatusCode);
    Assert.Equal(2, await db.ScheduledTrips.CountAsync()); Assert.Single(await db.BusRoutes.ToArrayAsync()); Assert.Equal(2, await db.ScheduledTripLogs.CountAsync()); Assert.Equal(2, await db.UploadRecords.CountAsync());
    Assert.Equal(2, await db.Uploads.CountAsync()); await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectTripImport");
    // Concurrent files using the same bus and interval cannot both create inventory.
    using var one = TripUpload(partner.Id, (later, [values])); using var two = TripUpload(partner.Id, (later, [values]));
    var concurrent = await Task.WhenAll(client.PostAsync("/api/partner/scheduled-trips/import", one), client.PostAsync("/api/partner/scheduled-trips/import", two));
    Assert.Single(concurrent, message => message.StatusCode == HttpStatusCode.OK); Assert.Single(concurrent, message => message.StatusCode == HttpStatusCode.BadRequest);
    foreach (var message in concurrent) message.Dispose(); Assert.Equal(3, await db.ScheduledTrips.CountAsync());
  }

  [SqlServerFact]
  public async Task ScheduledTripImportRejectsOverlapsAcrossSheetsStaleRecordsPastDatesAndMismatchedRoutesWithoutPartialRecords()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(3).Date); var first = date.ToString("yyyy-MM-dd"); var second = date.AddDays(1).ToString("yyyy-MM-dd");
    async Task<PartnerScheduledTripImportResult> Reject(params (string Name, string[][] Rows)[] sheets)
    {
      using var upload = TripUpload(partner.Id, sheets); using var response = await client.PostAsync("/api/partner/scheduled-trips/import", upload);
      Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); return (await response.Content.ReadFromJsonAsync<PartnerScheduledTripImportResult>())!;
    }
    var late = ScheduledTripExcelImportTests.Values(bus.Id, origin.Id, destination.Id, "23:30");
    var early = ScheduledTripExcelImportTests.Values(bus.Id, origin.Id, destination.Id, "00:30");
    Assert.Contains((await Reject((first, [late]), (second, [early]))).Errors, error => error.Sheet == second && error.Row == 2 && error.Field == "Bus");
    var normal = ScheduledTripExcelImportTests.Values(bus.Id, origin.Id, destination.Id);
    Assert.Contains((await Reject((first, [normal, ScheduledTripExcelImportTests.Values(bus.Id, origin.Id, destination.Id, "11:00", "121")]))).Errors, error => error.Field == "Duration (minutes)");
    Assert.Contains((await Reject((first, [ScheduledTripExcelImportTests.Values(Guid.NewGuid(), origin.Id, destination.Id)]))).Errors, error => error.Field == "Bus");
    Assert.Contains((await Reject((first, [ScheduledTripExcelImportTests.Values(bus.Id, Guid.NewGuid(), destination.Id)]))).Errors, error => error.Field == "Origin terminal");
    var trackedBus = await db.Buses.SingleAsync(); trackedBus.IsActive = false; await db.SaveChangesAsync();
    Assert.Contains((await Reject((first, [normal]))).Errors, error => error.Field == "Bus"); trackedBus.IsActive = true; await db.SaveChangesAsync();
    var trackedTerminal = await db.BusTerminals.Include(terminal => terminal.Location).SingleAsync(terminal => terminal.Id == destination.Id); trackedTerminal.Location.IsActive = false; await db.SaveChangesAsync();
    Assert.Contains((await Reject((first, [normal]))).Errors, error => error.Field == "Destination terminal"); trackedTerminal.Location.IsActive = true; await db.SaveChangesAsync();
    var yesterday = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).Date).AddDays(-1).ToString("yyyy-MM-dd");
    Assert.Contains((await Reject((yesterday, [normal]))).Errors, error => error.Field == "Date");
    Assert.Empty(await db.ScheduledTrips.ToArrayAsync()); Assert.Empty(await db.BusRoutes.ToArrayAsync()); Assert.Empty(await db.ScheduledTripLogs.ToArrayAsync()); Assert.Empty(await db.UploadRecords.ToArrayAsync());
    // Existing route and schedule are authoritative for subsequent uploads.
    using var accepted = TripUpload(partner.Id, (first, [normal])); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/partner/scheduled-trips/import", accepted)).StatusCode);
    Assert.Contains((await Reject((first, [normal]))).Errors, error => error.Field == "Bus");
    Assert.Contains((await Reject((second, [ScheduledTripExcelImportTests.Values(bus.Id, origin.Id, destination.Id, duration: "121")]))).Errors, error => error.Field == "Duration (minutes)");
    Assert.Single(await db.ScheduledTrips.ToArrayAsync());
  }

  [SqlServerFact]
  public async Task ScheduledTripImportAndTemplateRequireExactWriteRoleScopedMembershipAndNonPastDateRange()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var date = DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(3).Date).ToString("yyyy-MM-dd");
    string Url(Guid id, string start, string end) => $"/api/partner/scheduled-trips/template?partnerId={id}&startDate={start}&endDate={end}";
    var values = ScheduledTripExcelImportTests.Values(bus.Id, origin.Id, destination.Id);
    foreach (var roles in new object?[] { null, BusRoles("scheduled-trip.read"), BusRoles("bus.read", "bus.write") })
    {
      client.DefaultRequestHeaders.Authorization = roles is null ? null : new("Bearer", factory.Token(resourceAccess: roles));
      using var form = TripUpload(partner.Id, (date, [values])); var status = roles is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
      Assert.Equal(status, (await client.PostAsync("/api/partner/scheduled-trips/import", form)).StatusCode); Assert.Equal(status, (await client.GetAsync(Url(partner.Id, date, date))).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    using var foreign = TripUpload(Guid.NewGuid(), (date, [values])); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/scheduled-trips/import", foreign)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(Guid.NewGuid(), date, date))).StatusCode);
    foreach (var (start, end) in new[] { ("2020-01-01", date), (date, "2020-01-01"), (date, DateOnly.Parse(date).AddDays(31).ToString("yyyy-MM-dd")), ("not-a-date", date) })
      Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Url(partner.Id, start, end))).StatusCode);
    await using var db = CreateDatabase(); var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    using var inactive = TripUpload(partner.Id, (date, [values])); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/partner/scheduled-trips/import", inactive)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(partner.Id, date, date))).StatusCode);
    Assert.Empty(await db.Uploads.ToArrayAsync());
  }
}
