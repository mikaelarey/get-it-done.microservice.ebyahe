using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotScheduledTripTravelTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "ScheduledTrips",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE trip SET DurationMinutes = route.DurationMinutes
                FROM ScheduledTrips trip JOIN BusRoutes route ON route.Id = trip.BusRouteId
                WHERE trip.DurationMinutes IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScheduledTrips_Duration",
                table: "ScheduledTrips",
                sql: "[DurationMinutes] IS NULL OR [DurationMinutes] BETWEEN 1 AND 2880");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ScheduledTrips_Duration",
                table: "ScheduledTrips");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "ScheduledTrips");
        }
    }
}
