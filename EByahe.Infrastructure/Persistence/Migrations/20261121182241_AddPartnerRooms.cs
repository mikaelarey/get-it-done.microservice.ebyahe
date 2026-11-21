using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GuestCapacity = table.Column<int>(type: "int", nullable: false),
                    NightlyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, collation: "Latin1_General_100_BIN2"),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.CheckConstraint("CK_Rooms_Capacity", "[GuestCapacity] BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_Rooms_Deletion", "([IsDeleted] = 0 AND [DeletedBy] IS NULL AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [DeletedBy] IS NOT NULL AND [DeletedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_Rooms_Rate", "[NightlyRate] BETWEEN 0 AND 99999999.99");
                    table.ForeignKey(
                        name: "FK_Rooms_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rooms_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Rooms_RefUsers_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomLogs", x => x.Id);
                    table.CheckConstraint("CK_RoomLogs_Values", "[Action] IN ('Created','Updated','Deleted') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)");
                    table.ForeignKey(
                        name: "FK_RoomLogs_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomLogs_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomLogs_CreatedBy",
                table: "RoomLogs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RoomLogs_RoomId_DateTimeCreated",
                table: "RoomLogs",
                columns: new[] { "RoomId", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CreatedBy",
                table: "Rooms",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_DeletedBy",
                table: "Rooms",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_HotelId_Name",
                table: "Rooms",
                columns: new[] { "HotelId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomLogs");

            migrationBuilder.DropTable(
                name: "Rooms");
        }
    }
}
