using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditPartnerCashPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangedBy",
                table: "BusBookingStatusLogs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_ChangedBy",
                table: "BusBookingStatusLogs",
                column: "ChangedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookingStatusLogs_RefUsers_ChangedBy",
                table: "BusBookingStatusLogs",
                column: "ChangedBy",
                principalTable: "RefUsers",
                principalColumn: "ExternalUserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusBookingStatusLogs_RefUsers_ChangedBy",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_BusBookingStatusLogs_ChangedBy",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "ChangedBy",
                table: "BusBookingStatusLogs");
        }
    }
}
