using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessCategoryCodeDecode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplications_BusinessCategory",
                table: "PartnerApplications");

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBusinessCategory",
                table: "PartnerMemberRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBusinessCategory",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeDecodeBusinessCategories",
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
                    table.PrimaryKey("PK_CodeDecodeBusinessCategories", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeBusinessCategories_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeBusinessCategories_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeBusinessCategories",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("af7f5cca-4f4a-48b1-9770-21b7e779d3eb"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Hotel" },
                    { new Guid("c60b60a7-5b9e-40bf-9d06-8fb38a5263fd"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bus Operator" }
                });

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplications] WHERE [BusinessCategory] NOT IN ('bus-operator', 'hotel'))
                   OR EXISTS (SELECT 1 FROM [PartnerMemberRequests] WHERE [BusinessCategory] NOT IN ('bus-operator', 'hotel'))
                  THROW 51000, 'Unknown business category; correct the legacy value before migrating.', 1;
                UPDATE [PartnerApplications] SET [CodeDecodeBusinessCategory] = CASE [BusinessCategory] WHEN 'bus-operator' THEN 1 WHEN 'hotel' THEN 2 END;
                UPDATE [PartnerMemberRequests] SET [CodeDecodeBusinessCategory] = CASE [BusinessCategory] WHEN 'bus-operator' THEN 1 WHEN 'hotel' THEN 2 END;
                """);

            migrationBuilder.AlterColumn<int>(name: "CodeDecodeBusinessCategory", table: "PartnerApplications", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeBusinessCategory", table: "PartnerMemberRequests", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.DropColumn(
                name: "BusinessCategory",
                table: "PartnerMemberRequests");

            migrationBuilder.DropColumn(
                name: "BusinessCategory",
                table: "PartnerApplications");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerMemberRequests_CodeDecodeBusinessCategory",
                table: "PartnerMemberRequests",
                column: "CodeDecodeBusinessCategory");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeBusinessCategory",
                table: "PartnerApplications",
                column: "CodeDecodeBusinessCategory");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBusinessCategories_CreatedBy",
                table: "CodeDecodeBusinessCategories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBusinessCategories_Decode",
                table: "CodeDecodeBusinessCategories",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeBusinessCategories_CodeDecodeBusinessCategory",
                table: "PartnerApplications",
                column: "CodeDecodeBusinessCategory",
                principalTable: "CodeDecodeBusinessCategories",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerMemberRequests_CodeDecodeBusinessCategories_CodeDecodeBusinessCategory",
                table: "PartnerMemberRequests",
                column: "CodeDecodeBusinessCategory",
                principalTable: "CodeDecodeBusinessCategories",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessCategory",
                table: "PartnerMemberRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessCategory",
                table: "PartnerApplications",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplications] WHERE [CodeDecodeBusinessCategory] NOT IN (1, 2))
                   OR EXISTS (SELECT 1 FROM [PartnerMemberRequests] WHERE [CodeDecodeBusinessCategory] NOT IN (1, 2))
                  THROW 51000, 'A new business category cannot be represented by the previous schema.', 1;
                UPDATE [PartnerApplications] SET [BusinessCategory] = CASE [CodeDecodeBusinessCategory] WHEN 1 THEN 'bus-operator' WHEN 2 THEN 'hotel' END;
                UPDATE [PartnerMemberRequests] SET [BusinessCategory] = CASE [CodeDecodeBusinessCategory] WHEN 1 THEN 'bus-operator' WHEN 2 THEN 'hotel' END;
                """);
            migrationBuilder.AlterColumn<string>(name: "BusinessCategory", table: "PartnerApplications", type: "varchar(32)", unicode: false, maxLength: 32, nullable: false, oldClrType: typeof(string), oldType: "varchar(32)", oldUnicode: false, oldMaxLength: 32, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "BusinessCategory", table: "PartnerMemberRequests", type: "nvarchar(32)", maxLength: 32, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(32)", oldMaxLength: 32, oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeBusinessCategories_CodeDecodeBusinessCategory",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerMemberRequests_CodeDecodeBusinessCategories_CodeDecodeBusinessCategory",
                table: "PartnerMemberRequests");

            migrationBuilder.DropTable(
                name: "CodeDecodeBusinessCategories");

            migrationBuilder.DropIndex(
                name: "IX_PartnerMemberRequests_CodeDecodeBusinessCategory",
                table: "PartnerMemberRequests");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeBusinessCategory",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBusinessCategory",
                table: "PartnerMemberRequests");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBusinessCategory",
                table: "PartnerApplications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplications_BusinessCategory",
                table: "PartnerApplications",
                sql: "[BusinessCategory] IN ('hotel', 'bus-operator')");
        }
    }
}
