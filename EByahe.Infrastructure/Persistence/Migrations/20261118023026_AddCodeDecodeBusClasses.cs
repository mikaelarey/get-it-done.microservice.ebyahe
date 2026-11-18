using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeDecodeBusClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBusClass",
                table: "Buses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeDecodeBusClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeBusClasses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeBusClasses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeBusClasses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeBusClasses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("b055c1a5-0000-4000-8000-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "First Class" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Deluxe" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000003"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Super Deluxe" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000004"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Luxury" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000005"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Executive Class" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000006"), 6, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Executive Luxury" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000007"), 7, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Executive Solo" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000008"), 8, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Regular Aircon" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000009"), 9, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Regular / Air-conditioned" },
                    { new Guid("b055c1a5-0000-4000-8000-000000000010"), 10, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Air-conditioned / Direct" }
                });

            // Preserve every existing class, including labels created before the setup table.
            // No bus is assigned a default class during the conversion.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Buses] WHERE NULLIF(LTRIM(RTRIM([BusClass])), N'') IS NULL)
                    THROW 50001, 'Existing buses must have a bus class before applying AddCodeDecodeBusClasses.', 1;
                ;WITH LegacyClasses AS (
                    SELECT DISTINCT LTRIM(RTRIM([BusClass])) AS [Decode] FROM [Buses]
                )
                INSERT INTO [CodeDecodeBusClasses] ([Id], [Decode])
                SELECT NEWID(), legacy.[Decode] FROM LegacyClasses legacy
                WHERE NOT EXISTS (SELECT 1 FROM [CodeDecodeBusClasses] setup WHERE setup.[Decode] = legacy.[Decode]);
                UPDATE bus SET [CodeDecodeBusClass] = setup.[Code]
                FROM [Buses] bus JOIN [CodeDecodeBusClasses] setup ON setup.[Decode] = LTRIM(RTRIM(bus.[BusClass]));
                IF EXISTS (SELECT 1 FROM [Buses] WHERE [CodeDecodeBusClass] IS NULL)
                    THROW 50002, 'A bus class could not be mapped to its setup code.', 1;
                """);
            migrationBuilder.AlterColumn<int>(
                name: "CodeDecodeBusClass", table: "Buses", type: "int", nullable: false,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.DropColumn(name: "BusClass", table: "Buses");

            migrationBuilder.CreateIndex(
                name: "IX_Buses_CodeDecodeBusClass",
                table: "Buses",
                column: "CodeDecodeBusClass");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBusClasses_CreatedBy",
                table: "CodeDecodeBusClasses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBusClasses_Decode",
                table: "CodeDecodeBusClasses",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Buses_CodeDecodeBusClasses_CodeDecodeBusClass",
                table: "Buses",
                column: "CodeDecodeBusClass",
                principalTable: "CodeDecodeBusClasses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusClass", table: "Buses", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.Sql("""
                UPDATE bus SET [BusClass] = setup.[Decode]
                FROM [Buses] bus JOIN [CodeDecodeBusClasses] setup ON bus.[CodeDecodeBusClass] = setup.[Code];
                """);
            migrationBuilder.AlterColumn<string>(
                name: "BusClass", table: "Buses", type: "nvarchar(100)", maxLength: 100, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(100)", oldMaxLength: 100, oldNullable: true);
            migrationBuilder.DropForeignKey(
                name: "FK_Buses_CodeDecodeBusClasses_CodeDecodeBusClass",
                table: "Buses");

            migrationBuilder.DropTable(
                name: "CodeDecodeBusClasses");

            migrationBuilder.DropIndex(
                name: "IX_Buses_CodeDecodeBusClass",
                table: "Buses");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBusClass",
                table: "Buses");

        }
    }
}
