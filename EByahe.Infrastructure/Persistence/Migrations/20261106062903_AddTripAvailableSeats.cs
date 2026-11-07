using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripAvailableSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvailableSeats",
                table: "ScheduledTrips",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScheduledTrips_AvailableSeats",
                table: "ScheduledTrips",
                sql: "[AvailableSeats] IS NULL OR [AvailableSeats] BETWEEN 0 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ScheduledTrips_AvailableSeats",
                table: "ScheduledTrips");

            migrationBuilder.DropColumn(
                name: "AvailableSeats",
                table: "ScheduledTrips");
        }
    }
}
