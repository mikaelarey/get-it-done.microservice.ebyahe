using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneratePartnerTerminalRoutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BusRoutes_Duration",
                table: "BusRoutes");

            migrationBuilder.AlterColumn<int>(
                name: "DurationMinutes",
                table: "BusRoutes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusRoutes_Duration",
                table: "BusRoutes",
                sql: "[DurationMinutes] IS NULL OR [DurationMinutes] BETWEEN 1 AND 2880");

            migrationBuilder.Sql("""
                INSERT INTO BusRoutes (Id, PartnerId, OriginTerminalId, DestinationTerminalId, DurationMinutes, IsActive, IsDemo)
                SELECT NEWID(), origin.PartnerId, origin.Id, destination.Id, NULL, 1, 0
                FROM BusTerminals origin
                JOIN BusTerminals destination ON destination.PartnerId = origin.PartnerId AND destination.Id <> origin.Id
                WHERE NOT EXISTS (
                    SELECT 1 FROM BusRoutes route WHERE route.PartnerId = origin.PartnerId
                        AND route.OriginTerminalId = origin.Id AND route.DestinationTerminalId = destination.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Unconfigured pairs contain no travel-time data. Never invent a duration on rollback.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM BusRoutes route JOIN ScheduledTrips trip ON trip.BusRouteId = route.Id
                    WHERE route.DurationMinutes IS NULL)
                    THROW 51000, 'Configure travel times referenced by scheduled trips before rolling back.', 1;
                DELETE FROM BusRoutes WHERE DurationMinutes IS NULL;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_BusRoutes_Duration",
                table: "BusRoutes");

            migrationBuilder.AlterColumn<int>(
                name: "DurationMinutes",
                table: "BusRoutes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusRoutes_Duration",
                table: "BusRoutes",
                sql: "[DurationMinutes] > 0");
        }
    }
}
