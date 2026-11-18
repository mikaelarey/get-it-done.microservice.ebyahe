using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task BusTemplateDownloadRequiresWritePermissionUsesFreshSetupAndCanBeImported()
  {
    var partner = await SeedMemberPartner();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    const string path = "/api/partner/buses/template";
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    foreach (var roles in new[] { BusRoles("bus.read"), BusRoles("bus.write"), BookingRoles() })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("bus.read", "bus.write")));
    using var initial = await client.GetAsync(path);
    Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
    Assert.True(initial.Headers.CacheControl!.NoStore);
    Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", initial.Content.Headers.ContentType!.MediaType);
    Assert.Contains("Bus-import-template.xlsx", initial.Content.Headers.ContentDisposition!.ToString());
    Assert.Equal(10, BusExcelTemplateTests.Classes(await initial.Content.ReadAsByteArrayAsync()).Length);
    await using var db = CreateDatabase(); var added = new CodeDecodeBusClass { Decode = "Sleeper" };
    db.CodeDecodeBusClasses.Add(added); await db.SaveChangesAsync();
    Assert.Contains("Sleeper", BusExcelTemplateTests.Classes(await client.GetByteArrayAsync(path)));
    added.Decode = "Premium Sleeper"; await db.SaveChangesAsync();
    var current = await client.GetByteArrayAsync(path);
    var classes = BusExcelTemplateTests.Classes(current);
    Assert.Contains("Premium Sleeper", classes); Assert.DoesNotContain("Sleeper", classes);
    using var form = new MultipartFormDataContent(); form.Add(new StringContent(partner.Id.ToString()), "partnerId");
    form.Add(new ByteArrayContent(BusExcelTemplateTests.Fill(current, "Premium Sleeper")), "file", "buses.xlsx");
    using var imported = await client.PostAsync("/api/partner/buses/import", form);
    Assert.Equal(HttpStatusCode.OK, imported.StatusCode);
    Assert.Equal(1, (await imported.Content.ReadFromJsonAsync<PartnerBusImportResult>())!.ImportedCount);
    Assert.Equal(added.Code, (await db.Buses.AsNoTracking().SingleAsync()).CodeDecodeBusClass);
  }
}
