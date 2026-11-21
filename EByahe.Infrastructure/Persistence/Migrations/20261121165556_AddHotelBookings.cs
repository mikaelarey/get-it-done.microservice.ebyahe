using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HotelBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    GuestPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RoomName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RoomCount = table.Column<int>(type: "int", nullable: false),
                    GuestCount = table.Column<int>(type: "int", nullable: false),
                    CheckIn = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOut = table.Column<DateOnly>(type: "date", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodeDecodeBookingStatus = table.Column<int>(type: "int", nullable: false),
                    CodeDecodePaymentStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelBookings", x => x.Id);
                    table.CheckConstraint("CK_HotelBookings_Counts", "[RoomCount] BETWEEN 1 AND 100 AND [GuestCount] BETWEEN 1 AND 1000");
                    table.CheckConstraint("CK_HotelBookings_Stay", "[CheckOut] > [CheckIn]");
                    table.CheckConstraint("CK_HotelBookings_Total", "[Total] >= 0");
                    table.ForeignKey(
                        name: "FK_HotelBookings_CodeDecodeBookingStatuses_CodeDecodeBookingStatus",
                        column: x => x.CodeDecodeBookingStatus,
                        principalTable: "CodeDecodeBookingStatuses",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HotelBookings_CodeDecodePaymentStatuses_CodeDecodePaymentStatus",
                        column: x => x.CodeDecodePaymentStatus,
                        principalTable: "CodeDecodePaymentStatuses",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HotelBookings_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HotelBookings_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_CodeDecodeBookingStatus",
                table: "HotelBookings",
                column: "CodeDecodeBookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_CodeDecodePaymentStatus",
                table: "HotelBookings",
                column: "CodeDecodePaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_CreatedBy",
                table: "HotelBookings",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_HotelId_CreatedAtUtc",
                table: "HotelBookings",
                columns: new[] { "HotelId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_HotelBookings_Reference",
                table: "HotelBookings",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HotelBookings");
        }
    }
}
