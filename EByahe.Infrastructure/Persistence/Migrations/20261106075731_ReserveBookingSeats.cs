using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReserveBookingSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservedSeats",
                table: "BusBookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_ReservedSeats",
                table: "BusBookings",
                sql: "[ReservedSeats] BETWEEN 0 AND 20 AND ([ReservedSeats] = 0 OR [ScheduledTripId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_ReservedSeats",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "ReservedSeats",
                table: "BusBookings");
        }
    }
}
