using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventTypeCodeDecode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeEventType",
                table: "Outgoing",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeEventType",
                table: "OutboxMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeDecodeEventTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeEventTypes", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeEventTypes_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeEventTypes_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeEventTypes",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner-application.submitted" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner-application.under-review" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000003"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner-application.approved" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000004"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner-application.declined" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000005"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner.approved" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000006"), 6, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner.created" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000007"), 7, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner.updated" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000008"), 8, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner.member.requested" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000009"), 9, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "partner-user.updated" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000010"), 10, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "booking.created" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000011"), 11, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "bus.created" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000012"), 12, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "bus.updated" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000013"), 13, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "buses.imported" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000014"), 14, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "terminal.created" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000015"), 15, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "terminal.updated" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000016"), 16, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "terminals.imported" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000017"), 17, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "scheduled-trip.created" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000018"), 18, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "scheduled-trip.updated" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000019"), 19, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "scheduled-trips.imported" },
                    { new Guid("93ede9f3-4a5c-498e-8613-000000000020"), 20, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "users.imported" }
                });

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [OutboxMessages] source LEFT JOIN [CodeDecodeEventTypes] setup ON setup.[Decode]=source.[EventType] WHERE setup.[Code] IS NULL)
                   OR EXISTS (SELECT 1 FROM [Outgoing] source LEFT JOIN [CodeDecodeEventTypes] setup ON setup.[Decode]=source.[EventType] WHERE setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped event type; register its setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeEventType]=setup.[Code] FROM [OutboxMessages] source JOIN [CodeDecodeEventTypes] setup ON setup.[Decode]=source.[EventType];
                UPDATE source SET [CodeDecodeEventType]=setup.[Code] FROM [Outgoing] source JOIN [CodeDecodeEventTypes] setup ON setup.[Decode]=source.[EventType];
                """);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeEventType", table: "OutboxMessages", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeEventType", table: "Outgoing", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "Outgoing");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_CodeDecodeEventType",
                table: "Outgoing",
                column: "CodeDecodeEventType");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CodeDecodeEventType",
                table: "OutboxMessages",
                column: "CodeDecodeEventType");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeEventTypes_CreatedBy",
                table: "CodeDecodeEventTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeEventTypes_Decode",
                table: "CodeDecodeEventTypes",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxMessages_CodeDecodeEventTypes_CodeDecodeEventType",
                table: "OutboxMessages",
                column: "CodeDecodeEventType",
                principalTable: "CodeDecodeEventTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Outgoing_CodeDecodeEventTypes_CodeDecodeEventType",
                table: "Outgoing",
                column: "CodeDecodeEventType",
                principalTable: "CodeDecodeEventTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "Outgoing",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "OutboxMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE source SET [EventType]=setup.[Decode] FROM [OutboxMessages] source JOIN [CodeDecodeEventTypes] setup ON setup.[Code]=source.[CodeDecodeEventType];
                UPDATE source SET [EventType]=setup.[Decode] FROM [Outgoing] source JOIN [CodeDecodeEventTypes] setup ON setup.[Code]=source.[CodeDecodeEventType];
                """);
            migrationBuilder.AlterColumn<string>(name: "EventType", table: "OutboxMessages", type: "nvarchar(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(100)", oldMaxLength: 100, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "EventType", table: "Outgoing", type: "nvarchar(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(100)", oldMaxLength: 100, oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_OutboxMessages_CodeDecodeEventTypes_CodeDecodeEventType",
                table: "OutboxMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Outgoing_CodeDecodeEventTypes_CodeDecodeEventType",
                table: "Outgoing");

            migrationBuilder.DropTable(
                name: "CodeDecodeEventTypes");

            migrationBuilder.DropIndex(
                name: "IX_Outgoing_CodeDecodeEventType",
                table: "Outgoing");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_CodeDecodeEventType",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "CodeDecodeEventType",
                table: "Outgoing");

            migrationBuilder.DropColumn(
                name: "CodeDecodeEventType",
                table: "OutboxMessages");


        }
    }
}
