using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusPartnerFleetPlateUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Buses_PartnerId_FleetNumber_PlateNumber",
                table: "Buses",
                columns: new[] { "PartnerId", "FleetNumber", "PlateNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Buses_PartnerId_FleetNumber_PlateNumber",
                table: "Buses");
        }
    }
}
