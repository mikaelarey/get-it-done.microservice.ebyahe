using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task NewTerminalGeneratesBothDirectionsOnlyWithinPartnerAndTravelTimeRequiresConfiguration()
  {
    var (partner, bus, origin, destination) = await SeedPartnerTripWorkspace(); await using var db = CreateDatabase();
    var configured = new BusRoute { PartnerId = partner.Id, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 60 };
    db.BusRoutes.Add(configured);
    var foreign = new BusTerminal { Partner = new Partner { Code = "OTHER", DisplayName = "Other" }, LocationId = origin.LocationId, Name = "Other partner terminal", Address = "Address" };
    db.BusTerminals.Add(foreign); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("terminal.read", "terminal.write")));
    var command = new { partnerId = partner.Id, codeDecodeRegion = 3, codeDecodeProvince = 2, codeDecodeMunicipality = 1, codeDecodeBarangay = 1, name = "New terminal", address = "Address" };
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
    var routes = await db.BusRoutes.AsNoTracking().ToArrayAsync(); Assert.Equal(6, routes.Length);
    Assert.All(routes, route => { Assert.Equal(partner.Id, route.PartnerId); Assert.NotEqual(route.OriginTerminalId, route.DestinationTerminalId);
      Assert.NotEqual(foreign.Id, route.OriginTerminalId); Assert.NotEqual(foreign.Id, route.DestinationTerminalId); });
    Assert.Equal(60, routes.Single(route => route.Id == configured.Id).DurationMinutes);
    Assert.Equal(5, routes.Count(route => route.DurationMinutes == null));
    Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/partner/terminals", command)).StatusCode);
    Assert.Equal(6, await db.BusRoutes.CountAsync());
    // Trip writers cannot configure defaults through the scheduling endpoint.
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    var trip = new AddPartnerScheduledTrip(partner.Id, bus.Id, destination.Id, origin.Id, DateTimeOffset.UtcNow.AddDays(3), 90, 500);
    Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", trip)).StatusCode);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/travel-times")).StatusCode);
    Assert.Single((await client.GetFromJsonAsync<PartnerTripOptions>("/api/partner/scheduled-trips/options"))!.Routes);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("travel-time.read", "travel-time.write")));
    var pending = routes.Single(route => route.OriginTerminalId == destination.Id && route.DestinationTerminalId == origin.Id);
    Assert.Equal(5, (await client.GetFromJsonAsync<PartnerRecordsPage<PartnerTravelTime>>("/api/partner/travel-times?search=Not%20configured"))!.TotalCount);
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/scheduled-trips/options")).StatusCode);
    Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/partner/travel-times/" + pending.Id, new UpdatePartnerTravelTime(90, true))).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("scheduled-trip.read", "scheduled-trip.write")));
    Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/partner/scheduled-trips", trip)).StatusCode);
    Assert.Equal(90, (await db.ScheduledTrips.SingleAsync()).DurationMinutes);
    Assert.Equal(60, (await db.BusRoutes.AsNoTracking().SingleAsync(route => route.Id == configured.Id)).DurationMinutes);
  }

  [SqlServerFact]
  public async Task RouteMigrationBackfillsAllSamePartnerPairsPreservesConfiguredRoutesAndCanRerun()
  {
    await using var db = CreateDatabase(); var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261120123125_SnapshotScheduledTripTravelTimes");
    var (_, _, origin, destination) = await SeedPartnerTripWorkspace();
    var third = new BusTerminal { PartnerId = origin.PartnerId, LocationId = origin.LocationId, Name = "Inactive terminal", Address = "Address", IsActive = false };
    var foreignPartner = new Partner { Code = "FOREIGN", DisplayName = "Foreign" };
    var foreignOne = new BusTerminal { Partner = foreignPartner, LocationId = origin.LocationId, Name = "Foreign one", Address = "Address" };
    var foreignTwo = new BusTerminal { Partner = foreignPartner, LocationId = origin.LocationId, Name = "Foreign two", Address = "Address" };
    db.BusTerminals.AddRange(third, foreignOne, foreignTwo);
    var configured = new BusRoute { PartnerId = origin.PartnerId, OriginTerminalId = origin.Id, DestinationTerminalId = destination.Id, DurationMinutes = 75, IsActive = false };
    db.BusRoutes.Add(configured); await db.SaveChangesAsync();
    async Task Verify()
    {
      db.ChangeTracker.Clear(); var rows = await db.BusRoutes.AsNoTracking().Include(route => route.OriginTerminal).Include(route => route.DestinationTerminal).ToArrayAsync();
      Assert.Equal(8, rows.Length); Assert.Equal(7, rows.Count(route => route.DurationMinutes == null));
      Assert.All(rows, route => { Assert.Equal(route.PartnerId, route.OriginTerminal.PartnerId); Assert.Equal(route.PartnerId, route.DestinationTerminal.PartnerId); Assert.NotEqual(route.OriginTerminalId, route.DestinationTerminalId); });
      var original = rows.Single(route => route.Id == configured.Id); Assert.Equal(75, original.DurationMinutes); Assert.False(original.IsActive);
    }
    await migrator.MigrateAsync(); await Verify();
    await migrator.MigrateAsync(); await Verify();
    await migrator.MigrateAsync("20261120123125_SnapshotScheduledTripTravelTimes"); Assert.Single(await db.BusRoutes.AsNoTracking().ToArrayAsync());
    await migrator.MigrateAsync(); await Verify();
  }
}
