using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRefUsersAndKafkaTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Incoming",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsumerGroup = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                    Topic = table.Column<string>(type: "varchar(249)", unicode: false, maxLength: 249, nullable: false),
                    Partition = table.Column<int>(type: "int", nullable: false),
                    Offset = table.Column<long>(type: "bigint", nullable: false),
                    MessageKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ReceivedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incoming", x => x.Id);
                    table.CheckConstraint("CK_Incoming_Status", "[Status] IN ('Received', 'Acknowledged', 'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "varchar(249)", unicode: false, maxLength: 249, nullable: false),
                    MessageKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CreatedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LockToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessages_Status", "[Status] IN ('Pending', 'Publishing', 'Failed', 'Published')");
                });

            migrationBuilder.CreateTable(
                name: "RefUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    FirstName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, collation: "Latin1_General_100_CI_AS_SC"),
                    Provider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Get It Done"),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Normal", collation: "Latin1_General_100_BIN2"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefUsers", x => x.Id);
                    table.CheckConstraint("CK_RefUsers_Role", "[Role] IN ('Administrator', 'Normal')");
                });

            migrationBuilder.CreateTable(
                name: "Outgoing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "varchar(249)", unicode: false, maxLength: 249, nullable: false),
                    MessageKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RecordedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedDateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    Partition = table.Column<int>(type: "int", nullable: true),
                    Offset = table.Column<long>(type: "bigint", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outgoing", x => x.Id);
                    table.CheckConstraint("CK_Outgoing_Status", "[Status] IN ('Pending', 'Publishing', 'Failed', 'Published', 'LeaseExpired')");
                    table.ForeignKey(
                        name: "FK_Outgoing_OutboxMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "OutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_ConsumerGroup_Topic_Partition_Offset",
                table: "Incoming",
                columns: new[] { "ConsumerGroup", "Topic", "Partition", "Offset" });

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_DeliveryId_Status",
                table: "Incoming",
                columns: new[] { "DeliveryId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_MessageId",
                table: "Incoming",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_ReceivedDateTime",
                table: "Incoming",
                column: "ReceivedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptDateTime_CreatedDateTime",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptDateTime", "CreatedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_UserId",
                table: "OutboxMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_MessageId_AttemptCount_Status",
                table: "Outgoing",
                columns: new[] { "MessageId", "AttemptCount", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_PublishedDateTime",
                table: "Outgoing",
                column: "PublishedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_RecordedDateTime",
                table: "Outgoing",
                column: "RecordedDateTime");

            migrationBuilder.CreateIndex(
                name: "UX_RefUsers_Email",
                table: "RefUsers",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Incoming");

            migrationBuilder.DropTable(
                name: "Outgoing");

            migrationBuilder.DropTable(
                name: "RefUsers");

            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
