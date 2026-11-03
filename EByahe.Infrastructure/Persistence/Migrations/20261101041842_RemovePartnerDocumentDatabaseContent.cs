using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePartnerDocumentDatabaseContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplicationDocuments]
                  WHERE [Content] IS NOT NULL OR NULLIF([StorageBucket], '') IS NULL OR NULLIF([StorageKey], '') IS NULL)
                  THROW 51000, 'Run Ebyahe application initialization to copy and verify legacy documents before removing database content.', 1;
                IF EXISTS (
                  SELECT 1 FROM [PartnerApplicationHistories] h
                  CROSS APPLY (VALUES(h.[PreviousValue]), (h.[CurrentValue])) s([Payload])
                  CROSS APPLY OPENJSON(s.[Payload], '$.Documents') d
                  WHERE EXISTS (SELECT 1 FROM OPENJSON(d.[value]) p WHERE p.[key] = 'Content')
                    OR NULLIF(JSON_VALUE(d.[value], '$.StorageBucket'), '') IS NULL
                    OR NULLIF(JSON_VALUE(d.[value], '$.StorageKey'), '') IS NULL
                ) THROW 51000, 'Migrate historical document versions to verified storage references first.', 1;
                """);
            migrationBuilder.DropColumn(
                name: "Content",
                table: "PartnerApplicationDocuments");

            migrationBuilder.AlterColumn<string>(
                name: "StorageKey",
                table: "PartnerApplicationDocuments",
                type: "varchar(512)",
                unicode: false,
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(512)",
                oldUnicode: false,
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StorageBucket",
                table: "PartnerApplicationDocuments",
                type: "varchar(63)",
                unicode: false,
                maxLength: 63,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(63)",
                oldUnicode: false,
                oldMaxLength: 63,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StorageKey",
                table: "PartnerApplicationDocuments",
                type: "varchar(512)",
                unicode: false,
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(512)",
                oldUnicode: false,
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "StorageBucket",
                table: "PartnerApplicationDocuments",
                type: "varchar(63)",
                unicode: false,
                maxLength: 63,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(63)",
                oldUnicode: false,
                oldMaxLength: 63);

            migrationBuilder.AddColumn<byte[]>(
                name: "Content",
                table: "PartnerApplicationDocuments",
                type: "varbinary(max)",
                nullable: true);
        }
    }
}
