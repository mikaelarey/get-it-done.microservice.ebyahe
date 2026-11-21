using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeDecodeRoomTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeRoomType",
                table: "Rooms",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeDecodeRoomTypes",
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
                    table.PrimaryKey("PK_CodeDecodeRoomTypes", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeRoomTypes_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeRoomTypes_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeRoomTypes",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("c0de7001-0000-4000-8000-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Standard" },
                    { new Guid("c0de7001-0000-4000-8000-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Superior" },
                    { new Guid("c0de7001-0000-4000-8000-000000000003"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Deluxe" },
                    { new Guid("c0de7001-0000-4000-8000-000000000004"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Suite" },
                    { new Guid("c0de7001-0000-4000-8000-000000000005"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Family" },
                    { new Guid("c0de7001-0000-4000-8000-000000000006"), 6, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Studio" },
                    { new Guid("c0de7001-0000-4000-8000-000000000007"), 7, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Single" },
                    { new Guid("c0de7001-0000-4000-8000-000000000008"), 8, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Double" },
                    { new Guid("c0de7001-0000-4000-8000-000000000009"), 9, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Twin" },
                    { new Guid("c0de7001-0000-4000-8000-000000000010"), 10, "System", new DateTimeOffset(new DateTime(2026, 11, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Executive" }
                });

            // Preserve labels from all rooms, including soft-deleted records.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Rooms] WHERE NULLIF(LTRIM(RTRIM([RoomType])), N'') IS NULL)
                    THROW 50001, 'Existing rooms must have a room type before applying AddCodeDecodeRoomTypes.', 1;
                ;WITH LegacyTypes AS (
                    SELECT DISTINCT LTRIM(RTRIM([RoomType])) AS [Decode] FROM [Rooms]
                )
                INSERT INTO [CodeDecodeRoomTypes] ([Id], [Decode])
                SELECT NEWID(), legacy.[Decode] FROM LegacyTypes legacy
                WHERE NOT EXISTS (SELECT 1 FROM [CodeDecodeRoomTypes] setup WHERE setup.[Decode] = legacy.[Decode]);
                UPDATE room SET [CodeDecodeRoomType] = setup.[Code]
                FROM [Rooms] room JOIN [CodeDecodeRoomTypes] setup ON setup.[Decode] = LTRIM(RTRIM(room.[RoomType]));
                IF EXISTS (SELECT 1 FROM [Rooms] WHERE [CodeDecodeRoomType] IS NULL)
                    THROW 50002, 'A room type could not be mapped to its setup code.', 1;
                """);
            migrationBuilder.AlterColumn<int>(
                name: "CodeDecodeRoomType", table: "Rooms", type: "int", nullable: false,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.DropColumn(name: "RoomType", table: "Rooms");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CodeDecodeRoomType",
                table: "Rooms",
                column: "CodeDecodeRoomType");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeRoomTypes_CreatedBy",
                table: "CodeDecodeRoomTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeRoomTypes_Decode",
                table: "CodeDecodeRoomTypes",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_CodeDecodeRoomTypes_CodeDecodeRoomType",
                table: "Rooms",
                column: "CodeDecodeRoomType",
                principalTable: "CodeDecodeRoomTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoomType", table: "Rooms", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.Sql("""
                UPDATE room SET [RoomType] = setup.[Decode]
                FROM [Rooms] room JOIN [CodeDecodeRoomTypes] setup ON room.[CodeDecodeRoomType] = setup.[Code];
                """);
            migrationBuilder.AlterColumn<string>(
                name: "RoomType", table: "Rooms", type: "nvarchar(100)", maxLength: 100, nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(100)", oldMaxLength: 100, oldNullable: true);
            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_CodeDecodeRoomTypes_CodeDecodeRoomType",
                table: "Rooms");

            migrationBuilder.DropTable(
                name: "CodeDecodeRoomTypes");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_CodeDecodeRoomType",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "CodeDecodeRoomType",
                table: "Rooms");

        }
    }
}
