using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Reference = table.Column<string>(type: "varchar(35)", unicode: false, maxLength: 35, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusBookings", x => x.Id);
                    table.CheckConstraint("CK_BusBookings_DetailsJson", "ISJSON([DetailsJson]) = 1");
                    table.CheckConstraint("CK_BusBookings_Status", "[Status] = 'Submitted'");
                    table.CheckConstraint("CK_BusBookings_Total", "[Total] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_CreatedBy_CreatedAtUtc",
                table: "BusBookings",
                columns: new[] { "CreatedBy", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_CreatedBy_RequestId",
                table: "BusBookings",
                columns: new[] { "CreatedBy", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_Reference",
                table: "BusBookings",
                column: "Reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusBookings");
        }
    }
}
