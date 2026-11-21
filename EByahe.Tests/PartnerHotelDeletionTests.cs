using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EByahe.Domain.DataModels;
using EByahe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PartnerHotelDeletionRetainsTheRecordAndImmutableCreateUpdateDeleteLogs()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    var created = await client.PostAsJsonAsync("/api/partner/hotels", command);
    var hotel = (await created.Content.ReadFromJsonAsync<PartnerHotel>())!; var path = "/api/partner/hotels/" + hotel.Id;
    var update = HotelUpdate(command); update.Name = "Updated Hotel";
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path, update)).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path, update)).StatusCode);
    var page = (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels?search=Updated"))!;
    Assert.Empty(page.Items); Assert.Equal(0, page.TotalCount);
    await using var db = CreateDatabase(); var saved = await db.Hotels.AsNoTracking().SingleAsync();
    Assert.Equal(hotel.Id, saved.Id); Assert.True(saved.IsDeleted); Assert.NotNull(saved.DeletedAtUtc);
    Assert.Equal(PartnerApiFactory.Subject, saved.DeletedBy); Assert.Equal("Updated Hotel", saved.Name);
    var logs = await db.HotelLogs.AsNoTracking().OrderBy(log => log.DateTimeCreated).ToArrayAsync();
    Assert.Equal(new[] { "Created", "Updated", "Deleted" }, logs.Select(log => log.Action));
    Assert.All(logs, log => { Assert.Equal(hotel.Id, log.HotelId); Assert.Equal(PartnerApiFactory.Subject, log.CreatedBy); });
    Assert.Null(logs[0].PreviousValue);
    using var previousUpdate = JsonDocument.Parse(logs[1].PreviousValue!); using var currentUpdate = JsonDocument.Parse(logs[1].CurrentValue);
    Assert.Equal(command.Name, previousUpdate.RootElement.GetProperty("name").GetString()); Assert.Equal("Updated Hotel", currentUpdate.RootElement.GetProperty("name").GetString());
    using var previousDelete = JsonDocument.Parse(logs[2].PreviousValue!); using var currentDelete = JsonDocument.Parse(logs[2].CurrentValue);
    Assert.False(previousDelete.RootElement.GetProperty("isDeleted").GetBoolean()); Assert.True(currentDelete.RootElement.GetProperty("isDeleted").GetBoolean());
    Assert.Equal(PartnerApiFactory.Subject, currentDelete.RootElement.GetProperty("deletedBy").GetString());
    var replacement = await client.PostAsJsonAsync("/api/partner/hotels", command with { Name = "Updated Hotel" });
    Assert.Equal(HttpStatusCode.Created, replacement.StatusCode); Assert.NotEqual(hotel.Id, (await replacement.Content.ReadFromJsonAsync<PartnerHotel>())!.Id);
    Assert.Equal(2, await db.Hotels.CountAsync());
    Assert.Single((await client.GetFromJsonAsync<PartnerRecordsPage<PartnerHotel>>("/api/partner/hotels"))!.Items);

    var existingLog = await db.HotelLogs.FirstAsync(); existingLog.CurrentValue = "{}";
    await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
    db.HotelLogs.Remove(await db.HotelLogs.FirstAsync());
    await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
    db.Hotels.Remove(await db.Hotels.SingleAsync(row => row.Id == hotel.Id));
    await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
  }

  [SqlServerFact]
  public async Task PartnerHotelDeletionRequiresManagePermissionAndTheCorrectActiveCompanyMembership()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    var created = await client.PostAsJsonAsync("/api/partner/hotels", command);
    var id = (await created.Content.ReadFromJsonAsync<PartnerHotel>())!.Id; var path = "/api/partner/hotels/" + id;
    client.DefaultRequestHeaders.Authorization = null; Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(path)).StatusCode);
    foreach (var roles in new[] { BusRoles("hotel.read"), BusRoles("hotel.write"), BusRoles("room.read", "room.write"),
      new Dictionary<string, object> { ["get-it-done-web"] = new { roles = new[] { "partner", "hotel.read", "hotel.write" } } } })
    {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync(path)).StatusCode);
    }
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "other-hotel-user", resourceAccess: BusRoles("hotel.read", "hotel.write")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(path)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("hotel.read", "hotel.write")));
    await using var db = CreateDatabase(); var member = await db.PartnerUsers.SingleAsync(); member.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(path)).StatusCode);
    Assert.False((await db.Hotels.SingleAsync()).IsDeleted); Assert.Single(await db.HotelLogs.ToArrayAsync());
  }

  [SqlServerFact]
  public async Task PartnerHotelDeletionIsAtomicWithItsAuditEntryAndConcurrentRetriesLogOnce()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    Guid id;
    await using (var create = CreateDatabase()) id = (await new PartnerHotelRepository(create).AddAsync(PartnerApiFactory.Subject, command, default))!.Id;
    await using var db = CreateDatabase();
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectHotelAudit ON HotelLogs AFTER INSERT AS THROW 51066, 'Test hotel audit failure', 1;");
    try
    {
      await using var failed = CreateDatabase();
      await Assert.ThrowsAsync<DbUpdateException>(() => new PartnerHotelRepository(failed).DeleteAsync(PartnerApiFactory.Subject, id, default));
      Assert.False((await db.Hotels.AsNoTracking().SingleAsync()).IsDeleted); Assert.Equal(1, await db.HotelLogs.CountAsync());
    }
    finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectHotelAudit"); }
    async Task<bool> Delete() { await using var context = CreateDatabase(); return await new PartnerHotelRepository(context).DeleteAsync(PartnerApiFactory.Subject, id, default); }
    Assert.All(await Task.WhenAll(Delete(), Delete()), Assert.True);
    Assert.True((await db.Hotels.AsNoTracking().SingleAsync()).IsDeleted);
    Assert.Equal(1, await db.HotelLogs.CountAsync(log => log.Action == "Deleted"));
  }

  [SqlServerFact]
  public async Task PartnerHotelDeletionMigrationPreservesExistingHotelsAndAddsABaselineLog()
  {
    var partner = await SeedMemberPartner("hotel"); var command = await HotelCommand(partner.Id);
    await using var db = CreateDatabase(); var migrator = db.GetService<IMigrator>();
    // This is an isolated test database. Recreate the pre-feature schema to exercise the real upgrade.
    await migrator.MigrateAsync("20261121142311_AddPartnerHotels");
    var id = Guid.NewGuid(); var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO Hotels (Id, PartnerId, Name, Address, CodeDecodeRegion, CodeDecodeProvince, CodeDecodeMunicipality,
        CodeDecodeBarangay, ContactNumber, Email, Description, IsActive, CreatedBy, CreatedAtUtc)
      VALUES ({id}, {partner.Id}, {command.Name}, {command.Address}, {command.CodeDecodeRegion}, {command.CodeDecodeProvince},
        {command.CodeDecodeMunicipality}, {command.CodeDecodeBarangay}, {command.ContactNumber}, {command.Email}, {command.Description},
        {true}, {PartnerApiFactory.Subject}, {createdAt})
      """);
    await migrator.MigrateAsync();
    var hotel = await db.Hotels.SingleAsync(); Assert.Equal(id, hotel.Id); Assert.False(hotel.IsDeleted);
    Assert.Equal(command.Email, hotel.Email); Assert.Equal(createdAt, hotel.CreatedAtUtc); Assert.Equal(PartnerApiFactory.Subject, hotel.CreatedBy);
    var baseline = await db.HotelLogs.SingleAsync(); Assert.Equal("Baseline", baseline.Action); Assert.Equal("System", baseline.CreatedBy);
    Assert.Equal(id, baseline.HotelId); Assert.Null(baseline.PreviousValue);
    using var snapshot = JsonDocument.Parse(baseline.CurrentValue); Assert.Equal(command.Name, snapshot.RootElement.GetProperty("name").GetString());
    Assert.False(snapshot.RootElement.GetProperty("isDeleted").GetBoolean()); Assert.False(db.Database.HasPendingModelChanges());
  }
}
