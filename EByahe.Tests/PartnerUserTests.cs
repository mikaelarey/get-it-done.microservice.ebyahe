using EByahe.Domain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  private static RefUser MembershipUser(string subject) => new()
  {
    ExternalUserId = subject, FirstName = "Partner", LastName = "Member",
    Email = subject + "@example.com", CreatedAtUtc = DateTimeOffset.UtcNow
  };

  [SqlServerFact]
  public async Task PartnersCanHaveMultipleUsersAndUsersCanBelongToMultiplePartners()
  {
    await using var db = CreateDatabase();
    var first = new Partner { Code = "FIRST", DisplayName = "First partner" };
    var second = new Partner { Code = "SECOND", DisplayName = "Second partner" };
    var alice = MembershipUser("alice"); var bob = MembershipUser("bob"); var carol = MembershipUser("carol");
    var inactive = new PartnerUser { Partner = first, RefUser = carol, IsActive = false };
    db.PartnerUsers.AddRange(new PartnerUser { Partner = first, RefUser = alice },
      new PartnerUser { Partner = first, RefUser = bob }, inactive,
      new PartnerUser { Partner = second, RefUser = alice });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var partners = await db.Partners.Include(x => x.UserMemberships).ThenInclude(x => x.RefUser).ToArrayAsync();
    Assert.Equal(3, partners.Single(x => x.Id == first.Id).UserMemberships.Count);
    Assert.Equal(2, (await db.RefUsers.Include(x => x.PartnerMemberships).SingleAsync(x => x.Id == alice.Id)).PartnerMemberships.Count);
    var saved = await db.PartnerUsers.SingleAsync(x => x.Id == inactive.Id);
    Assert.False(saved.IsActive); Assert.NotEqual(Guid.Empty, saved.Id); Assert.Equal(TimeSpan.Zero, saved.CreatedAtUtc.Offset);
    var created = saved.CreatedAtUtc;
    saved.IsActive = true; await db.SaveChangesAsync(); saved.IsActive = false; await db.SaveChangesAsync();
    Assert.Equal(4, await db.PartnerUsers.CountAsync()); Assert.Equal(created, saved.CreatedAtUtc);
    Assert.All(await db.RefUsers.ToArrayAsync(), u => Assert.Equal(RefUser.NormalRole, u.Role));
    Assert.False(db.Database.HasPendingModelChanges());
  }

  [SqlServerFact]
  public async Task MembershipPairIsUniqueEvenWhenInactiveAndBothForeignKeysAreEnforced()
  {
    await using var db = CreateDatabase();
    var partner = new Partner { Code = "UNIQUE", DisplayName = "Unique partner" }; var user = MembershipUser("member");
    var membership = new PartnerUser { Partner = partner, RefUser = user, IsActive = false };
    db.PartnerUsers.Add(membership); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    db.PartnerUsers.Add(new PartnerUser { PartnerId = partner.Id, RefUserId = user.Id });
    var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    Assert.Contains(((SqlException)duplicate.InnerException!).Number, new[] { 2601, 2627 });
    db.ChangeTracker.Clear();
    foreach (var invalid in new[] { new PartnerUser { PartnerId = Guid.NewGuid(), RefUserId = user.Id },
      new PartnerUser { PartnerId = partner.Id, RefUserId = Guid.NewGuid() } })
    {
      db.PartnerUsers.Add(invalid);
      var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
      Assert.Equal(547, ((SqlException)failure.InnerException!).Number); db.ChangeTracker.Clear();
    }
    Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
      $"DELETE FROM [Partners] WHERE [Id] = {partner.Id}"))).Number);
    Assert.Equal(547, (await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
      $"DELETE FROM [RefUsers] WHERE [Id] = {user.Id}"))).Number);
    Assert.Equal(membership.Id, (await db.PartnerUsers.SingleAsync()).Id);
  }

  [SqlServerFact]
  public async Task PartnerUserMigrationPreservesExistingOwnersAndBackfillsExactLocalIdentitiesOnce()
  {
    await using var db = CreateDatabase();
    await ResetToMigrationAsync(db, "20261106091705_ConvertApplicationKeysToGuids");
    var exact = MembershipUser("OwnerCase"); var different = MembershipUser("ownercase");
    // Email uniqueness is case-insensitive while identity subjects are case-sensitive.
    different.Email = "different@example.com";
    db.RefUsers.AddRange(exact, different);
    var owned = new Partner { Code = "OWNED", DisplayName = "Owned", OwnerUserId = exact.ExternalUserId };
    var unowned = new Partner { Code = "UNOWNED", DisplayName = "Unowned" };
    db.Partners.AddRange(owned, unowned); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await db.Database.MigrateAsync();
    var member = await db.PartnerUsers.SingleAsync();
    Assert.Equal(owned.Id, member.PartnerId); Assert.Equal(exact.Id, member.RefUserId); Assert.True(member.IsActive);
    Assert.Equal("OwnerCase", (await db.Partners.SingleAsync(x => x.Id == owned.Id)).OwnerUserId);
    Assert.Equal(2, await db.RefUsers.CountAsync()); Assert.Equal(2, await db.Partners.CountAsync());
    await db.Database.MigrateAsync(); Assert.Equal(1, await db.PartnerUsers.CountAsync());
  }

  [SqlServerFact]
  public async Task MissingLegacyOwnerReferenceStopsMigrationWithoutInventingUsersOrChangingPartners()
  {
    await using var db = CreateDatabase();
    await ResetToMigrationAsync(db, "20261106091705_ConvertApplicationKeysToGuids");
    var partner = new Partner { Code = "MISSING", DisplayName = "Missing reference", OwnerUserId = "missing-owner" };
    db.Partners.Add(partner); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var failure = await Assert.ThrowsAsync<SqlException>(() => db.Database.MigrateAsync());
    Assert.Equal(51030, failure.Number); Assert.Empty(await db.RefUsers.ToArrayAsync());
    Assert.Equal(partner.Id, (await db.Partners.SingleAsync()).Id);
    Assert.Contains("20261106134748_AddPartnerUsers", await db.Database.GetPendingMigrationsAsync());
    db.RefUsers.Add(MembershipUser("missing-owner")); await db.SaveChangesAsync();
    await db.Database.MigrateAsync(); Assert.Equal(1, await db.PartnerUsers.CountAsync());
  }
}
