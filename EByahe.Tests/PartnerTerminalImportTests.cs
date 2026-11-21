using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static MultipartFormDataContent TerminalUpload(Guid partnerId, params string[][] rows)
  {
    var form = new MultipartFormDataContent(); form.Add(new StringContent(partnerId.ToString()), "partnerId");
    form.Add(new ByteArrayContent(TerminalExcelImportTests.Workbook([TerminalExcelImportTests.Headers, .. rows])), "file", "terminals.xlsx");
    return form;
  }
  [SqlServerFact]
  public async Task TerminalImportValidatesEveryHierarchyAndCommitsLocationsTerminalsAndEventsTogether()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    const string api = "/api/partner/terminals/import";
    var initialEvents = await db.OutboxMessages.CountAsync(); var initialLocations = await db.Locations.CountAsync();
    using var valid = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid(), TerminalExcelImportTests.Valid("Other terminal"));
    using var response = await client.PostAsync(api, valid);
    Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
    Assert.Equal(2, (await response.Content.ReadFromJsonAsync<PartnerTerminalImportResult>())!.ImportedCount);
    Assert.Equal(2, await db.BusTerminals.CountAsync()); Assert.Equal(initialLocations + 1, await db.Locations.CountAsync());
    Assert.Equal(2, await db.BusRoutes.CountAsync()); Assert.All(await db.BusRoutes.ToArrayAsync(), route => Assert.Null(route.DurationMinutes));
    Assert.Equal(initialEvents + 1, await db.OutboxMessages.CountAsync());
    var summary = Assert.Single(await db.OutboxMessages.Where(row => row.EventType == "terminals.imported").ToArrayAsync());
    using var payload = JsonDocument.Parse(summary.Payload);
    Assert.Equal("Successfully created 2 terminals using file upload.", payload.RootElement.GetProperty("message").GetString());
    Assert.Equal(2, payload.RootElement.GetProperty("details").GetProperty("recordCount").GetInt32());
    var upload = Assert.Single(await db.Uploads.ToArrayAsync());
    Assert.Equal(UploadTypeCodes.Terminals, upload.UploadType); Assert.Equal(PartnerApiFactory.Subject, upload.CreatedBy);
    Assert.Equal(upload.Id, payload.RootElement.GetProperty("details").GetProperty("uploadId").GetGuid());
    Assert.Equal(2, await db.UploadRecords.CountAsync(row => row.UploadId == upload.Id && row.TerminalId != null));
    var logs = await db.TerminalLogs.ToArrayAsync(); Assert.Equal(2, logs.Length);
    Assert.All(logs, log => { Assert.Equal("Imported", log.Action); Assert.Null(log.PreviousValue); Assert.Equal(upload.Id, log.UploadId); Assert.Equal(PartnerApiFactory.Subject, log.CreatedBy); });
    Assert.Empty(await db.OutboxMessages.Where(row => row.EventType == "terminal.created").ToArrayAsync());
    var location = await db.Locations.SingleAsync(row => row.CodeDecodeBarangay == 1);
    Assert.Equal("Adams (Ilocos Norte)", location.Name); Assert.Equal(3, location.CodeDecodeRegion);
    using var duplicate = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid("Not saved"), TerminalExcelImportTests.Valid());
    using var rejected = await client.PostAsync(api, duplicate); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    var duplicateErrors = (await rejected.Content.ReadFromJsonAsync<PartnerTerminalImportResult>())!;
    Assert.Equal(0, duplicateErrors.ImportedCount); Assert.Contains(duplicateErrors.Errors, error => error.Row == 3 && error.Field == "Terminal name");
    var mismatch = TerminalExcelImportTests.Valid("Invalid province"); mismatch[2] = "1 - Metro Manila";
    using var invalid = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid("Not saved either"), mismatch);
    using var badRequest = await client.PostAsync(api, invalid); Assert.Equal(HttpStatusCode.BadRequest, badRequest.StatusCode);
    Assert.Contains((await badRequest.Content.ReadFromJsonAsync<PartnerTerminalImportResult>())!.Errors, error => error.Row == 3 && error.Field == "Province");
    Assert.Equal(2, await db.BusTerminals.CountAsync()); Assert.Equal(initialEvents + 1, await db.OutboxMessages.CountAsync());
    location.IsActive = false; await db.SaveChangesAsync();
    using var inactive = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid("Inactive terminal"));
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(api, inactive)).StatusCode);
    location.IsActive = true; await db.SaveChangesAsync();
    var other = TerminalExcelImportTests.Valid("Atomic terminal"); other[3] = "2 - Bacarra"; other[4] = "2 - Bani";
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectTerminalImport ON dbo.OutboxMessages AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.CodeDecodeEventTypes et ON et.Code=i.CodeDecodeEventType WHERE et.Decode='terminals.imported') THROW 51061,'Test outbox failure',1;");
    using var failed = TerminalUpload(partner.Id, other);
    Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync(api, failed)).StatusCode);
    Assert.Equal(5, await db.Uploads.CountAsync()); Assert.Equal(2, await db.UploadRecords.CountAsync()); Assert.Equal(2, await db.TerminalLogs.CountAsync());
    Assert.Equal(2, await db.BusTerminals.CountAsync()); Assert.Equal(initialLocations + 1, await db.Locations.CountAsync());
    Assert.Equal(2, await db.BusRoutes.CountAsync()); Assert.All(await db.BusRoutes.ToArrayAsync(), route => Assert.Null(route.DurationMinutes));
    Assert.Equal(initialEvents + 1, await db.OutboxMessages.CountAsync());
    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectTerminalImport");
    using var retry = TerminalUpload(partner.Id, other); Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(api, retry)).StatusCode);
    Assert.Equal(initialLocations + 2, await db.Locations.CountAsync());
    Assert.Equal(6, await db.BusRoutes.CountAsync());
    Assert.Equal(initialEvents + 2, await db.OutboxMessages.CountAsync());
    Assert.Equal(6, await db.Uploads.CountAsync()); Assert.Equal(3, await db.UploadRecords.CountAsync()); Assert.Equal(3, await db.TerminalLogs.CountAsync());
    var messages = await db.OutboxMessages.Where(row => row.EventType == "terminals.imported").Select(row => row.Payload).ToArrayAsync();
    Assert.Contains(messages, message => { using var json = JsonDocument.Parse(message); return json.RootElement.GetProperty("message").GetString() == "Successfully created 1 terminal using file upload."; });
  }
  [SqlServerFact]
  public async Task TerminalImportAndManualCreationShareLocationsDuringConcurrentRequests()
  {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    using var form = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid("Excel terminal"));
    var command = new { partnerId = partner.Id, codeDecodeRegion = 3, codeDecodeProvince = 2, codeDecodeMunicipality = 1,
      codeDecodeBarangay = 1, name = "Manual terminal", address = "100 Main street" };
    var responses = await Task.WhenAll(client.PostAsync("/api/partner/terminals/import", form), client.PostAsJsonAsync("/api/partner/terminals", command));
    using (responses[0]) Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
    using (responses[1]) Assert.Equal(HttpStatusCode.Created, responses[1].StatusCode);
    await using var db = CreateDatabase(); Assert.Single(await db.Locations.ToArrayAsync()); Assert.Equal(2, await db.BusTerminals.CountAsync());
    Assert.Equal(2, await db.BusRoutes.CountAsync());
  }
  [SqlServerFact]
  public async Task TerminalImportRequiresTerminalWriteRoleAndActiveApprovedMembership()
  {
    var partner = await SeedMemberPartner(); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string api = "/api/partner/terminals/import";
    foreach (var roles in new object?[] { null, BusRoles("terminal.read"), BusRoles("bus.read", "bus.write") })
    {
      client.DefaultRequestHeaders.Authorization = roles is null ? null : new("Bearer", factory.Token(resourceAccess: roles));
      using var form = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid());
      Assert.Equal(roles is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, (await client.PostAsync(api, form)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    using var foreign = TerminalUpload(Guid.NewGuid(), TerminalExcelImportTests.Valid()); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(api, foreign)).StatusCode);
    await using var db = CreateDatabase(); var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    using var inactive = TerminalUpload(partner.Id, TerminalExcelImportTests.Valid()); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(api, inactive)).StatusCode);
    Assert.Empty(await db.BusTerminals.ToArrayAsync());
    Assert.Empty(await db.Uploads.ToArrayAsync()); Assert.Empty(await db.UploadRecords.ToArrayAsync()); Assert.Empty(await db.TerminalLogs.ToArrayAsync());
  }
}
