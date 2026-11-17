using EByahe.Domain.Models;
using EByahe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace EByahe.Tests;
internal static class AuditActorFixture
{
  public static void Add(string connection, params string[] subjects)
  {
    using var db = new EByaheDbContext(new DbContextOptionsBuilder<EByaheDbContext>().UseSqlServer(connection).Options);
    foreach (var subject in subjects.Distinct())
      if (!db.RefUsers.Any(x => x.ExternalUserId == subject))
        db.RefUsers.Add(new RefUser { ExternalUserId = subject, FirstName = "Test", LastName = "Actor", Email = Guid.NewGuid().ToString("N") + "@example.com", Provider = "Test audit", CreatedAtUtc = DateTimeOffset.UtcNow });
    db.SaveChanges();
  }
}
