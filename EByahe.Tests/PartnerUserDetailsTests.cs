using System.Net;
using System.Net.Http.Json;
using EByahe.Domain.DataModels;
using EByahe.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task PartnerUserDetailsRequireMemberViewAccessAndStayWithinActiveApprovedMemberships()
  {
    var partner = await SeedMemberPartner(); await using var db = CreateDatabase();
    var user = MembershipUser("details-user"); user.FirstName = "Maria"; user.LastName = "Santos";
    user.Email = "maria@example.com"; user.CreatedAtUtc = new DateTimeOffset(2026, 11, 1, 2, 30, 0, TimeSpan.Zero);
    var membership = new PartnerUser { PartnerId = partner.Id, RefUser = user, IsActive = false,
      CreatedAtUtc = new DateTimeOffset(2026, 11, 5, 6, 0, 0, TimeSpan.Zero) };
    var foreignPartner = new Partner { Code = "USER-DETAILS-FOREIGN", DisplayName = "Other workspace" };
    var foreignMembership = new PartnerUser { Partner = foreignPartner, RefUser = user };
    db.PartnerUsers.AddRange(membership, foreignMembership); await db.SaveChangesAsync();
    using var factory = new PartnerApiFactory(ConnectionString); using var client = factory.AuthenticatedClient();
    var path = $"/api/partner/users/{membership.Id}";
    client.DefaultRequestHeaders.Authorization = null;
    Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    foreach (var roles in new[] { BusRoles("booking.read"), new Dictionary<string, object> {
      ["get-it-done-web"] = new { roles = new[] { "partner", "member.manage" } } } }) {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: roles));
      Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }
    foreach (var role in new[] { "member.create", "member.manage" }) {
      client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles(role)));
      using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      Assert.True(response.Headers.CacheControl!.NoStore);
      var details = (await response.Content.ReadFromJsonAsync<PartnerPortalUserDetails>())!;
      Assert.Equal(membership.Id, details.Id); Assert.Equal("Maria", details.FirstName); Assert.Equal("Santos", details.LastName);
      Assert.Equal("maria@example.com", details.Email); Assert.Equal(partner.DisplayName, details.Partner);
      Assert.False(details.IsActive); Assert.Equal(RefUser.ManualProvider, details.Provider);
      Assert.Equal(membership.CreatedAtUtc, details.JoinedAtUtc); Assert.Equal(user.CreatedAtUtc, details.AccountCreatedAtUtc);
      var json = await response.Content.ReadAsStringAsync();
      Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
      Assert.DoesNotContain("externalUserId", json); Assert.DoesNotContain("requestId", json);
      Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/users/{foreignMembership.Id}")).StatusCode);
      Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partner/users/{Guid.NewGuid()}")).StatusCode);
    }
    user.FirstName = "Updated"; await db.SaveChangesAsync();
    Assert.Equal("Updated", (await client.GetFromJsonAsync<PartnerPortalUserDetails>(path))!.FirstName);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(subject: "stranger", resourceAccess: BusRoles("member.manage")));
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "?userId=" + PartnerApiFactory.Subject)).StatusCode);
    client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(resourceAccess: BusRoles("member.manage")));
    var viewer = await db.PartnerUsers.SingleAsync(row => row.RefUser.ExternalUserId == PartnerApiFactory.Subject);
    viewer.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    viewer.IsActive = true; var storedPartner = await db.Partners.FindAsync(partner.Id); storedPartner!.Status = "Suspended";
    await db.SaveChangesAsync(); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
  }
}
