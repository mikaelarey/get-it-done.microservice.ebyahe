using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeIncomingAndTrackProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incoming_ConsumerGroup_Topic_Partition_Offset",
                table: "Incoming");

            migrationBuilder.DropIndex(
                name: "IX_Incoming_DeliveryId_Status",
                table: "Incoming");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "Incoming",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedDateTime",
                table: "Incoming",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IncomingLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncomingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RecordedDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomingLogs", x => x.Id);
                    table.CheckConstraint("CK_IncomingLogs_Status", "[Status] IN ('Received', 'Processing', 'Processed', 'Failed', 'Acknowledged', 'AcknowledgementFailed')");
                    table.ForeignKey(
                        name: "FK_IncomingLogs_Incoming_IncomingId",
                        column: x => x.IncomingId,
                        principalTable: "Incoming",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve every legacy status entry, while keeping one envelope per
            // Kafka coordinate. A legacy acknowledgement only recorded transport
            // receipt; it must not be treated as a completed user projection.
            migrationBuilder.Sql("""
                SELECT [Id] AS [SourceId],
                  FIRST_VALUE([Id]) OVER (
                    PARTITION BY [ConsumerGroup], [Topic], [Partition], [Offset]
                    ORDER BY [ReceivedDateTime], [RecordedDateTime], [Id]) AS [IncomingId]
                INTO #IncomingMapping
                FROM [Incoming];

                INSERT INTO [IncomingLogs]
                  ([Id], [IncomingId], [DeliveryId], [AttemptCount], [Status], [RecordedDateTime], [Error])
                SELECT i.[Id], m.[IncomingId], i.[DeliveryId], 0, i.[Status], i.[RecordedDateTime], i.[Error]
                FROM [Incoming] i JOIN #IncomingMapping m ON i.[Id] = m.[SourceId];

                DELETE i FROM [Incoming] i JOIN #IncomingMapping m ON i.[Id] = m.[SourceId]
                WHERE m.[SourceId] <> m.[IncomingId];

                UPDATE [Incoming] SET [Status] = 'Received', [Error] = NULL,
                  [RecordedDateTime] = [ReceivedDateTime];
                DROP TABLE #IncomingMapping;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_ConsumerGroup_Topic_Partition_Offset",
                table: "Incoming",
                columns: new[] { "ConsumerGroup", "Topic", "Partition", "Offset" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming",
                sql: "[Status] IN ('Received', 'Processing', 'Processed', 'Failed')");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingLogs_IncomingId_RecordedDateTime",
                table: "IncomingLogs",
                columns: new[] { "IncomingId", "RecordedDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original schema cannot represent processing states. Preserve
            // each envelope as a receipt when rolling back the processing feature.
            migrationBuilder.Sql("""
                UPDATE [Incoming] SET [Status] = 'Received', [Error] = NULL;
                """);

            migrationBuilder.DropTable(
                name: "IncomingLogs");

            migrationBuilder.DropIndex(
                name: "IX_Incoming_ConsumerGroup_Topic_Partition_Offset",
                table: "Incoming");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "Incoming");

            migrationBuilder.DropColumn(
                name: "ProcessedDateTime",
                table: "Incoming");

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_ConsumerGroup_Topic_Partition_Offset",
                table: "Incoming",
                columns: new[] { "ConsumerGroup", "Topic", "Partition", "Offset" });

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_DeliveryId_Status",
                table: "Incoming",
                columns: new[] { "DeliveryId", "Status" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming",
                sql: "[Status] IN ('Received', 'Acknowledged', 'Failed')");
        }
    }
}
