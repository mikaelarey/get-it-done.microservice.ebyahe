using EByahe.Domain.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EByahe.Tests;

public sealed partial class SqlServerDatabaseTests
{
  [SqlServerFact]
  public async Task BusinessCategoryMigrationPreservesBothCategoriesAndRequestsAndRejectsUnknownLegacyValues()
  {
    await using var db = CreateDatabase();
    var bus = ReviewApplication("Category bus", "bus-operator", "Approved", 1, "category-bus-owner");
    var hotel = ReviewApplication("Category hotel", "hotel", "Approved", 2, "category-hotel-owner");
    var partner = new Partner { Id = Guid.NewGuid(), Code = "CATEGORY-TEST", DisplayName = "Category test", PartnerApplicationId = bus.Id };
    var busRequest = new PartnerMemberRequest { Id = Guid.NewGuid(), Partner = partner, CreatedBy = "System", BusinessCategory = "bus-operator", Email = "bus@example.com", CreatedAtUtc = DateTimeOffset.UtcNow };
    var hotelRequest = new PartnerMemberRequest { Id = Guid.NewGuid(), Partner = partner, CreatedBy = "System", BusinessCategory = "hotel", Email = "hotel@example.com", CreatedAtUtc = DateTimeOffset.UtcNow };
    db.PartnerApplications.AddRange(bus, hotel); db.Partners.Add(partner);
    db.PartnerMemberRequests.AddRange(busRequest, hotelRequest); await db.SaveChangesAsync();
    var historyCount = await db.PartnerApplicationHistories.CountAsync();
    db.ChangeTracker.Clear();
    var migrations = db.Database.GetMigrations().ToArray();
    var categoryMigration = Array.FindIndex(migrations, id => id.EndsWith("_AddBusinessCategoryCodeDecode", StringComparison.Ordinal));
    Assert.True(categoryMigration > 0);
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync(migrations[categoryMigration - 1]);
    Assert.Equal(new[] { "bus-operator", "hotel" }, await db.Database.SqlQueryRaw<string>("SELECT [BusinessCategory] AS [Value] FROM [PartnerApplications] ORDER BY [BusinessCategory]").ToArrayAsync());
    Assert.Equal(new[] { "bus-operator", "hotel" }, await db.Database.SqlQueryRaw<string>("SELECT [BusinessCategory] AS [Value] FROM [PartnerMemberRequests] ORDER BY [BusinessCategory]").ToArrayAsync());

    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [PartnerMemberRequests] SET [BusinessCategory]='unsupported' WHERE [Id]={hotelRequest.Id}");
    var invalid = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync());
    Assert.Equal(51000, invalid.Number);
    Assert.DoesNotContain(migrations[categoryMigration], await db.Database.GetAppliedMigrationsAsync());
    Assert.Equal("unsupported", await db.Database.SqlQuery<string>($"SELECT [BusinessCategory] AS [Value] FROM [PartnerMemberRequests] WHERE [Id]={hotelRequest.Id}").SingleAsync());
    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [PartnerMemberRequests] SET [BusinessCategory]='hotel' WHERE [Id]={hotelRequest.Id}");
    await migrator.MigrateAsync();

    Assert.False(db.Database.HasPendingModelChanges());
    Assert.Equal(new[] { "Bus Operator", "Hotel" }, await db.CodeDecodeBusinessCategories.OrderBy(x => x.Code).Select(x => x.Decode).ToArrayAsync());
    Assert.Equal(1, (await db.PartnerApplications.SingleAsync(x => x.BusinessCategory == "bus-operator")).CodeDecodeBusinessCategory);
    Assert.Equal(2, (await db.PartnerApplications.SingleAsync(x => x.BusinessCategory == "hotel")).CodeDecodeBusinessCategory);
    Assert.Equal(new[] { "bus-operator", "hotel" }, await db.PartnerMemberRequests.OrderBy(x => x.CodeDecodeBusinessCategory).Select(x => x.BusinessCategory).ToArrayAsync());
    Assert.Equal(historyCount, await db.PartnerApplicationHistories.CountAsync());
    var unknownCode = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [PartnerApplications] SET [CodeDecodeBusinessCategory]=999 WHERE [Id]={bus.Id}"));
    Assert.Equal(547, unknownCode.Number);
    var usedCategory = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM [CodeDecodeBusinessCategories] WHERE [Code]=1"));
    Assert.Equal(547, usedCategory.Number);
  }
}
