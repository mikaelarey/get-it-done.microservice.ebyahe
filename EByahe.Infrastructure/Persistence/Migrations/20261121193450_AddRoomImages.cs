using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    StorageBucket = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, collation: "Latin1_General_100_BIN2"),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomImages", x => x.Id);
                    table.CheckConstraint("CK_RoomImages_Deletion", "([IsDeleted] = 0 AND [DeletedBy] IS NULL AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [DeletedBy] IS NOT NULL AND [DeletedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_RoomImages_Size", "[SizeBytes] > 0 AND [SizeBytes] <= 5242880");
                    table.CheckConstraint("CK_RoomImages_Type", "[ContentType] IN ('image/jpeg','image/png','image/webp')");
                    table.ForeignKey(
                        name: "FK_RoomImages_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomImages_RefUsers_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomImages_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomImages_CreatedBy",
                table: "RoomImages",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RoomImages_DeletedBy",
                table: "RoomImages",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_RoomImages_RoomId_SortOrder",
                table: "RoomImages",
                columns: new[] { "RoomId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomImages");
        }
    }
}
