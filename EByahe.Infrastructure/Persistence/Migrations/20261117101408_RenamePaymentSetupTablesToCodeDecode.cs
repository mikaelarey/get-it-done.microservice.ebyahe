using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePaymentSetupTablesToCodeDecode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_PaymentStatuses_PaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_PaymentTypes_PaymentType",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_Statuses_Status",
                table: "BusBookings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Statuses",
                table: "Statuses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentTypes",
                table: "PaymentTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaymentStatuses",
                table: "PaymentStatuses");

            migrationBuilder.RenameTable(
                name: "Statuses",
                newName: "CodeDecodeBookingStatuses");

            migrationBuilder.RenameTable(
                name: "PaymentTypes",
                newName: "CodeDecodePaymentTypes");

            migrationBuilder.RenameTable(
                name: "PaymentStatuses",
                newName: "CodeDecodePaymentStatuses");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CodeDecodeBookingStatuses",
                table: "CodeDecodeBookingStatuses",
                column: "Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CodeDecodePaymentTypes",
                table: "CodeDecodePaymentTypes",
                column: "Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CodeDecodePaymentStatuses",
                table: "CodeDecodePaymentStatuses",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_CodeDecodeBookingStatuses_Status",
                table: "BusBookings",
                column: "Status",
                principalTable: "CodeDecodeBookingStatuses",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentStatuses_PaymentStatus",
                table: "BusBookings",
                column: "PaymentStatus",
                principalTable: "CodeDecodePaymentStatuses",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentTypes_PaymentType",
                table: "BusBookings",
                column: "PaymentType",
                principalTable: "CodeDecodePaymentTypes",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_CodeDecodeBookingStatuses_Status",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentStatuses_PaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentTypes_PaymentType",
                table: "BusBookings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CodeDecodePaymentTypes",
                table: "CodeDecodePaymentTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CodeDecodePaymentStatuses",
                table: "CodeDecodePaymentStatuses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CodeDecodeBookingStatuses",
                table: "CodeDecodeBookingStatuses");

            migrationBuilder.RenameTable(
                name: "CodeDecodePaymentTypes",
                newName: "PaymentTypes");

            migrationBuilder.RenameTable(
                name: "CodeDecodePaymentStatuses",
                newName: "PaymentStatuses");

            migrationBuilder.RenameTable(
                name: "CodeDecodeBookingStatuses",
                newName: "Statuses");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentTypes",
                table: "PaymentTypes",
                column: "Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaymentStatuses",
                table: "PaymentStatuses",
                column: "Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Statuses",
                table: "Statuses",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_PaymentStatuses_PaymentStatus",
                table: "BusBookings",
                column: "PaymentStatus",
                principalTable: "PaymentStatuses",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_PaymentTypes_PaymentType",
                table: "BusBookings",
                column: "PaymentType",
                principalTable: "PaymentTypes",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_Statuses_Status",
                table: "BusBookings",
                column: "Status",
                principalTable: "Statuses",
                principalColumn: "Name",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
