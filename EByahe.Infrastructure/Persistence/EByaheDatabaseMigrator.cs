using EByahe.Infrastructure.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EByahe.Infrastructure.Storage;

namespace EByahe.Infrastructure.Persistence;

public sealed class EByaheDatabaseMigrator(EByaheDbContext database, PartnerDocumentStorageMigration storageMigration) : IDatabaseMigrator
{
  public async Task MigrateAsync(CancellationToken cancellationToken = default)
  {
    var prepare = database.Database.GetMigrations().Single(name => name.EndsWith("_AddPartnerDocumentStorageReferences", StringComparison.Ordinal));
    var applied = await database.Database.GetAppliedMigrationsAsync(cancellationToken);
    if (!applied.Contains(prepare)) await database.GetService<IMigrator>().MigrateAsync(prepare, cancellationToken);
    await storageMigration.MigrateAsync(cancellationToken);
    await database.Database.MigrateAsync(cancellationToken);
  }
}
