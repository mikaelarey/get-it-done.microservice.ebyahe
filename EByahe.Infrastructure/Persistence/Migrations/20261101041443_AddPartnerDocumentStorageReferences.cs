using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerDocumentStorageReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationDocuments_SizeBytes",
                table: "PartnerApplicationDocuments");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Content",
                table: "PartnerApplicationDocuments",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AddColumn<string>(
                name: "StorageBucket",
                table: "PartnerApplicationDocuments",
                type: "varchar(63)",
                unicode: false,
                maxLength: 63,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "PartnerApplicationDocuments",
                type: "varchar(512)",
                unicode: false,
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationDocuments_SizeBytes",
                table: "PartnerApplicationDocuments",
                sql: "[SizeBytes] > 0 AND [SizeBytes] <= 10485760");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplicationDocuments] WHERE [Content] IS NULL)
                  THROW 51000, 'Restore document bytes from storage before dropping the storage references.', 1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationDocuments_SizeBytes",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropColumn(
                name: "StorageBucket",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "PartnerApplicationDocuments");

            migrationBuilder.AlterColumn<byte[]>(
                name: "Content",
                table: "PartnerApplicationDocuments",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: new byte[0],
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationDocuments_SizeBytes",
                table: "PartnerApplicationDocuments",
                sql: "[SizeBytes] > 0 AND [SizeBytes] <= 10485760 AND DATALENGTH([Content]) = [SizeBytes]");
        }
    }
}
