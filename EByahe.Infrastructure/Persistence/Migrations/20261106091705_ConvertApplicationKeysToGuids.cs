using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations;

public partial class ConvertApplicationKeysToGuids : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    using var stream = typeof(ConvertApplicationKeysToGuids).Assembly.GetManifestResourceStream(
      "EByahe.Infrastructure.Persistence.Migrations.Scripts.ConvertApplicationKeysToGuids.sql")
      ?? throw new InvalidOperationException("The GUID conversion script is missing.");
    migrationBuilder.Sql(new StreamReader(stream).ReadToEnd());
  }

  protected override void Down(MigrationBuilder migrationBuilder) =>
    throw new NotSupportedException("GUID key conversion cannot be reversed without the verified pre-migration database backup. Restore the backup with the matching application version instead.");
}
