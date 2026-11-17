using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPaymentStatusesAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_ReservedSeats",
                table: "BusBookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings");

            migrationBuilder.AddColumn<bool>(
                name: "PaymentReviewRequired",
                table: "BusBookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "BusBookings",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending Payment");

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "BusBookings",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusBookingStatusLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PreviousPaymentStatus = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    PaymentStatus = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    PaymentType = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusBookingStatusLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusBookingStatusLogs_BusBookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "BusBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentStatuses",
                columns: table => new
                {
                    Name = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentStatuses", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTypes",
                columns: table => new
                {
                    Name = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTypes", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Statuses",
                columns: table => new
                {
                    Name = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Statuses", x => x.Name);
                });

            migrationBuilder.InsertData(
                table: "PaymentStatuses",
                column: "Name",
                values: new object[]
                {
                    "Cancelled",
                    "Payment Failed",
                    "Payment Success",
                    "Pending Payment"
                });

            migrationBuilder.InsertData(
                table: "PaymentTypes",
                column: "Name",
                values: new object[]
                {
                    "Cash",
                    "Credit/Debit Card",
                    "GCash",
                    "Maya"
                });

            migrationBuilder.InsertData(
                table: "Statuses",
                column: "Name",
                values: new object[]
                {
                    "Cancelled",
                    "Confirmed",
                    "Submitted"
                });

            // Preserve paid confirmations and release the old, unpaid submission-time reservations.
            migrationBuilder.Sql("""
                UPDATE [BusBookings] SET [PaymentStatus] = 'Payment Success' WHERE [Status] = 'Confirmed';
                UPDATE [BusBookings] SET [PaymentStatus] = 'Cancelled' WHERE [Status] = 'Cancelled';
                UPDATE [BusBookings] SET [PaymentType] = CASE
                  WHEN JSON_VALUE([DetailsJson], '$.payment.type') IN ('GCash', 'Maya', 'Credit/Debit Card', 'Cash')
                    THEN JSON_VALUE([DetailsJson], '$.payment.type')
                  WHEN JSON_VALUE([DetailsJson], '$.payment.method') = 'cash' THEN 'Cash'
                  WHEN JSON_VALUE([DetailsJson], '$.payment.method') = 'card' THEN 'Credit/Debit Card'
                  WHEN JSON_VALUE([DetailsJson], '$.payment.provider') IN ('GCash', 'Maya') THEN JSON_VALUE([DetailsJson], '$.payment.provider')
                  ELSE NULL END;
                INSERT INTO [BusBookingStatusLogs] ([Id], [BookingId], [PreviousStatus], [Status], [PreviousPaymentStatus], [PaymentStatus], [PaymentType], [ChangedAtUtc], [EventId], [Remarks])
                SELECT NEWID(), [Id], NULL, [Status], NULL, [PaymentStatus], [PaymentType], SYSUTCDATETIME(), NULL,
                  CASE WHEN [Status] <> 'Confirmed' AND [ReservedSeats] > 0
                    THEN CONCAT('Migrated existing booking; released ', [ReservedSeats], ' unpaid reserved seats.')
                    ELSE 'Migrated existing booking status; payment type remains unknown when the original request did not identify it.' END
                FROM [BusBookings];
                UPDATE trip SET [AvailableSeats] = CASE WHEN trip.[AvailableSeats] + released.[Seats] > bus.[SeatCapacity]
                  THEN bus.[SeatCapacity] ELSE trip.[AvailableSeats] + released.[Seats] END
                FROM [ScheduledTrips] trip JOIN [Buses] bus ON bus.[Id] = trip.[BusId]
                JOIN (SELECT [ScheduledTripId], SUM([ReservedSeats]) [Seats] FROM [BusBookings]
                  WHERE [Status] <> 'Confirmed' AND [ReservedSeats] > 0 GROUP BY [ScheduledTripId]) released
                  ON released.[ScheduledTripId] = trip.[Id]
                WHERE trip.[AvailableSeats] IS NOT NULL;
                UPDATE [BusBookings] SET [ReservedSeats] = 0 WHERE [Status] <> 'Confirmed';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_PaymentStatus",
                table: "BusBookings",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_PaymentType",
                table: "BusBookings",
                column: "PaymentType");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_Status",
                table: "BusBookings",
                column: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_CancelledPayment",
                table: "BusBookings",
                sql: "([Status] = 'Cancelled' AND [PaymentStatus] = 'Cancelled') OR ([Status] <> 'Cancelled' AND [PaymentStatus] <> 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_ConfirmedPayment",
                table: "BusBookings",
                sql: "[Status] <> 'Confirmed' OR ([PaymentStatus] = 'Payment Success' AND [ReservedSeats] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_ReservedSeats",
                table: "BusBookings",
                sql: "[ReservedSeats] BETWEEN 0 AND 20 AND ([ReservedSeats] = 0 OR ([ScheduledTripId] IS NOT NULL AND [Status] = 'Confirmed' AND [PaymentStatus] = 'Payment Success'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings",
                sql: "[Status] IN ('Submitted', 'Confirmed', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_BookingId_ChangedAtUtc",
                table: "BusBookingStatusLogs",
                columns: new[] { "BookingId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_EventId",
                table: "BusBookingStatusLogs",
                column: "EventId",
                unique: true,
                filter: "[EventId] IS NOT NULL");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropTable(
                name: "BusBookingStatusLogs");

            migrationBuilder.DropTable(
                name: "PaymentStatuses");

            migrationBuilder.DropTable(
                name: "PaymentTypes");

            migrationBuilder.DropTable(
                name: "Statuses");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_PaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_PaymentType",
                table: "BusBookings");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_Status",
                table: "BusBookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_CancelledPayment",
                table: "BusBookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_ConfirmedPayment",
                table: "BusBookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_ReservedSeats",
                table: "BusBookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "PaymentReviewRequired",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "BusBookings");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_ReservedSeats",
                table: "BusBookings",
                sql: "[ReservedSeats] BETWEEN 0 AND 20 AND ([ReservedSeats] = 0 OR [ScheduledTripId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings",
                sql: "[Status] IN ('Submitted', 'Confirmed')");
        }
    }
}
