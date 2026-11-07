using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduledTripId",
                table: "BusBookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Partners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true, collation: "Latin1_General_100_BIN2"),
                    PartnerApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partners", x => x.Id);
                    table.CheckConstraint("CK_Partners_Status", "[Status] IN ('Approved', 'Suspended')");
                    table.ForeignKey(
                        name: "FK_Partners_PartnerApplications_PartnerApplicationId",
                        column: x => x.PartnerApplicationId,
                        principalTable: "PartnerApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Buses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    FleetNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BusClass = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SeatCapacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buses", x => x.Id);
                    table.UniqueConstraint("AK_Buses_Id_PartnerId", x => new { x.Id, x.PartnerId });
                    table.CheckConstraint("CK_Buses_Capacity", "[SeatCapacity] BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_Buses_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusTerminals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusTerminals", x => x.Id);
                    table.UniqueConstraint("AK_BusTerminals_Id_PartnerId", x => new { x.Id, x.PartnerId });
                    table.ForeignKey(
                        name: "FK_BusTerminals_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusTerminals_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusRoutes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    OriginTerminalId = table.Column<int>(type: "int", nullable: false),
                    DestinationTerminalId = table.Column<int>(type: "int", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusRoutes", x => x.Id);
                    table.UniqueConstraint("AK_BusRoutes_Id_PartnerId", x => new { x.Id, x.PartnerId });
                    table.CheckConstraint("CK_BusRoutes_DifferentTerminals", "[OriginTerminalId] <> [DestinationTerminalId]");
                    table.CheckConstraint("CK_BusRoutes_Duration", "[DurationMinutes] > 0");
                    table.ForeignKey(
                        name: "FK_BusRoutes_BusTerminals_DestinationTerminalId_PartnerId",
                        columns: x => new { x.DestinationTerminalId, x.PartnerId },
                        principalTable: "BusTerminals",
                        principalColumns: new[] { "Id", "PartnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusRoutes_BusTerminals_OriginTerminalId_PartnerId",
                        columns: x => new { x.OriginTerminalId, x.PartnerId },
                        principalTable: "BusTerminals",
                        principalColumns: new[] { "Id", "PartnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusRoutes_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTrips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    BusId = table.Column<int>(type: "int", nullable: false),
                    BusRouteId = table.Column<int>(type: "int", nullable: false),
                    DepartureAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BaseFare = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsDemo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTrips", x => x.Id);
                    table.CheckConstraint("CK_ScheduledTrips_Fare", "[BaseFare] > 0");
                    table.CheckConstraint("CK_ScheduledTrips_Status", "[Status] IN ('Scheduled', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ScheduledTrips_BusRoutes_BusRouteId_PartnerId",
                        columns: x => new { x.BusRouteId, x.PartnerId },
                        principalTable: "BusRoutes",
                        principalColumns: new[] { "Id", "PartnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledTrips_Buses_BusId_PartnerId",
                        columns: x => new { x.BusId, x.PartnerId },
                        principalTable: "Buses",
                        principalColumns: new[] { "Id", "PartnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledTrips_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_ScheduledTripId",
                table: "BusBookings",
                column: "ScheduledTripId");

            migrationBuilder.CreateIndex(
                name: "IX_Buses_PartnerId_FleetNumber",
                table: "Buses",
                columns: new[] { "PartnerId", "FleetNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusRoutes_DestinationTerminalId_PartnerId",
                table: "BusRoutes",
                columns: new[] { "DestinationTerminalId", "PartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusRoutes_OriginTerminalId_PartnerId",
                table: "BusRoutes",
                columns: new[] { "OriginTerminalId", "PartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusRoutes_PartnerId_OriginTerminalId_DestinationTerminalId",
                table: "BusRoutes",
                columns: new[] { "PartnerId", "OriginTerminalId", "DestinationTerminalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusTerminals_LocationId",
                table: "BusTerminals",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_BusTerminals_PartnerId_LocationId_Name",
                table: "BusTerminals",
                columns: new[] { "PartnerId", "LocationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Code",
                table: "Locations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_Code",
                table: "Partners",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_PartnerApplicationId",
                table: "Partners",
                column: "PartnerApplicationId",
                unique: true,
                filter: "[PartnerApplicationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTrips_BusId_DepartureAtUtc",
                table: "ScheduledTrips",
                columns: new[] { "BusId", "DepartureAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTrips_BusId_PartnerId",
                table: "ScheduledTrips",
                columns: new[] { "BusId", "PartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTrips_BusRouteId_PartnerId",
                table: "ScheduledTrips",
                columns: new[] { "BusRouteId", "PartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTrips_PartnerId",
                table: "ScheduledTrips",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTrips_Status_DepartureAtUtc_BusRouteId",
                table: "ScheduledTrips",
                columns: new[] { "Status", "DepartureAtUtc", "BusRouteId" });

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_ScheduledTrips_ScheduledTripId",
                table: "BusBookings",
                column: "ScheduledTripId",
                principalTable: "ScheduledTrips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_ScheduledTrips_ScheduledTripId",
                table: "BusBookings");

            migrationBuilder.DropTable(
                name: "ScheduledTrips");

            migrationBuilder.DropTable(
                name: "BusRoutes");

            migrationBuilder.DropTable(
                name: "Buses");

            migrationBuilder.DropTable(
                name: "BusTerminals");

            migrationBuilder.DropTable(
                name: "Locations");

            migrationBuilder.DropTable(
                name: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_ScheduledTripId",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "ScheduledTripId",
                table: "BusBookings");
        }
    }
}
