using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeCodeDecode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationDocuments_PartnerApplicationId_DocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationDocuments_DocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeDecodeDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeDocumentTypes", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeDocumentTypes_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeDocumentTypes_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeDocumentTypes",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("075dc7bd-87b3-4766-b600-0fb753b81d81"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Registration" },
                    { new Guid("2184c469-e520-4a58-af41-3a66cb536e6f"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Accreditation" },
                    { new Guid("2f4a63db-e528-4ae9-a41b-fe606e520814"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bank Account Proof" },
                    { new Guid("4271b5bd-ab67-4797-8239-e0b953bc7c93"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Permit" },
                    { new Guid("d6437bc2-66c5-4e52-9458-a197dcb68f6b"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "BIR" }
                });

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplicationDocuments] WHERE [DocumentType] NOT IN ('registration', 'bir', 'permit', 'accreditation', 'bankAccountProof'))
                  THROW 51000, 'Unknown document type; correct the legacy value before migrating.', 1;
                UPDATE [PartnerApplicationDocuments] SET [CodeDecodeDocumentType] = CASE [DocumentType]
                  WHEN 'registration' THEN 1 WHEN 'bir' THEN 2 WHEN 'permit' THEN 3 WHEN 'accreditation' THEN 4 WHEN 'bankAccountProof' THEN 5 END;
                """);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeDocumentType", table: "PartnerApplicationDocuments", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationDocuments_CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments",
                column: "CodeDecodeDocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationDocuments_PartnerApplicationId_CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments",
                columns: new[] { "PartnerApplicationId", "CodeDecodeDocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeDocumentTypes_CreatedBy",
                table: "CodeDecodeDocumentTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeDocumentTypes_Decode",
                table: "CodeDecodeDocumentTypes",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplicationDocuments_CodeDecodeDocumentTypes_CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments",
                column: "CodeDecodeDocumentType",
                principalTable: "CodeDecodeDocumentTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "PartnerApplicationDocuments",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplicationDocuments] WHERE [CodeDecodeDocumentType] NOT IN (1, 2, 3, 4, 5))
                  THROW 51000, 'A new document type cannot be represented by the previous schema.', 1;
                UPDATE [PartnerApplicationDocuments] SET [DocumentType] = CASE [CodeDecodeDocumentType]
                  WHEN 1 THEN 'registration' WHEN 2 THEN 'bir' WHEN 3 THEN 'permit' WHEN 4 THEN 'accreditation' WHEN 5 THEN 'bankAccountProof' END;
                """);
            migrationBuilder.AlterColumn<string>(name: "DocumentType", table: "PartnerApplicationDocuments", type: "varchar(32)", unicode: false, maxLength: 32, nullable: false, oldClrType: typeof(string), oldType: "varchar(32)", oldUnicode: false, oldMaxLength: 32, oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplicationDocuments_CodeDecodeDocumentTypes_CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropTable(
                name: "CodeDecodeDocumentTypes");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationDocuments_CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationDocuments_PartnerApplicationId_CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropColumn(
                name: "CodeDecodeDocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationDocuments_PartnerApplicationId_DocumentType",
                table: "PartnerApplicationDocuments",
                columns: new[] { "PartnerApplicationId", "DocumentType" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationDocuments_DocumentType",
                table: "PartnerApplicationDocuments",
                sql: "[DocumentType] IN ('registration', 'bir', 'permit', 'accreditation', 'bankAccountProof')");
        }
    }
}
