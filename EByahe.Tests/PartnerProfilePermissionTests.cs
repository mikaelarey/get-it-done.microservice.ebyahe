using System.Net;
using System.Net.Http.Json;
using EByahe.Application.Controllers;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task ProfilePermissionsFollowHotelMembershipWithoutRequiringMemberManagementRoles()
  {
    await SeedMemberPartner("hotel"); using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("booking.read", "bus.read")));
    var response = (await client.GetFromJsonAsync<PartnerProfilePermissions>("/api/partner/profile/permissions"))!;
    Assert.Equal(new[] { "booking.confirm", "booking.read", "hotel.read", "hotel.write", "member.create", "member.manage", "room.read", "room.write" }, response.AvailableRoles);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "unrelated-partner", resourceAccess: BusRoles("hotel.read")));
    Assert.Empty((await client.GetFromJsonAsync<PartnerProfilePermissions>("/api/partner/profile/permissions"))!.AvailableRoles);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: MemberRoles(partner: false)));
    Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/partner/profile/permissions")).StatusCode);
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/partner/profile/permissions")).StatusCode);
  }

  [SqlServerFact]
  public async Task ProfilePermissionsCombineActiveApprovedCompaniesAndRemoveInactiveMemberships()
  {
    var hotel = await SeedMemberPartner("hotel");
    await using var db = CreateDatabase();
    var member = await db.PartnerUsers.SingleAsync(membership => membership.PartnerId == hotel.Id);
    var application = new PartnerApplication { Id = Guid.NewGuid(), CreatedBy = PartnerApiFactory.Subject,
      TrackingId = "APP-" + Guid.NewGuid().ToString("N"), Status = "Approved", BusinessCategory = "bus-operator",
      EntityType = "corporation", SettlementFrequency = "daily", BusinessName = "Bus company", SubmittedAtUtc = DateTimeOffset.UtcNow };
    var bus = new Partner { Code = "PROFILE-" + Guid.NewGuid().ToString("N"), DisplayName = "Bus company", Status = "Approved", PartnerApplicationId = application.Id };
    db.PartnerApplications.Add(application); db.Partners.Add(bus);
    var busMembership = new PartnerUser { Partner = bus, RefUserId = member.RefUserId };
    db.PartnerUsers.Add(busMembership); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("booking.read")));
    async Task<IReadOnlyList<string>> Roles() => (await client.GetFromJsonAsync<PartnerProfilePermissions>("/api/partner/profile/permissions"))!.AvailableRoles;
    Assert.Equal(PartnerMemberRoles.Allowed("hotel").Concat(PartnerMemberRoles.Allowed("bus-operator")).Distinct().Order(), await Roles());
    busMembership.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(PartnerMemberRoles.Allowed("hotel").Order(), await Roles());
    member.IsActive = false; await db.SaveChangesAsync();
    Assert.Empty(await Roles());
    member.IsActive = true;
    var persistedHotel = await db.Partners.SingleAsync(partner => partner.Id == hotel.Id);
    persistedHotel.Status = "Suspended"; await db.SaveChangesAsync();
    Assert.Empty(await Roles());
  }
}
