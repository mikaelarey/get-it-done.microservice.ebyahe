using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPaymentConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAtUtc",
                table: "BusBookings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                table: "BusBookings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPaymentId",
                table: "BusBookings",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_PaymentId",
                table: "BusBookings",
                column: "PaymentId",
                unique: true,
                filter: "[PaymentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_ProviderPaymentId",
                table: "BusBookings",
                column: "ProviderPaymentId",
                unique: true,
                filter: "[ProviderPaymentId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings",
                sql: "[Status] IN ('Submitted', 'Confirmed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BusBookings_PaymentId",
                table: "BusBookings");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_ProviderPaymentId",
                table: "BusBookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentId",
                table: "BusBookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings",
                sql: "[Status] = 'Submitted'");
        }
    }
}
