using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogAuditAndUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodeDecodeUploadTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeUploadTypes", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeUploadTypes_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeUploadTypes_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Uploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UploadType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uploads_CodeDecodeUploadTypes_UploadType",
                        column: x => x.UploadType,
                        principalTable: "CodeDecodeUploadTypes",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Uploads_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BusLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusLogs", x => x.Id);
                    table.CheckConstraint("CK_BusLogs_Values", "[Action] IN ('Created','Updated','Imported') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)");
                    table.ForeignKey(
                        name: "FK_BusLogs_Buses_BusId",
                        column: x => x.BusId,
                        principalTable: "Buses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusLogs_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusLogs_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledTripLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTripLogs", x => x.Id);
                    table.CheckConstraint("CK_ScheduledTripLogs_Values", "[Action] IN ('Created','Updated','Imported') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)");
                    table.ForeignKey(
                        name: "FK_ScheduledTripLogs_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledTripLogs_ScheduledTrips_ScheduledTripId",
                        column: x => x.ScheduledTripId,
                        principalTable: "ScheduledTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledTripLogs_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TerminalLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TerminalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerminalLogs", x => x.Id);
                    table.CheckConstraint("CK_TerminalLogs_Values", "[Action] IN ('Created','Updated','Imported') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)");
                    table.ForeignKey(
                        name: "FK_TerminalLogs_BusTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "BusTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerminalLogs_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TerminalLogs_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UploadRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TerminalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadRecords", x => x.Id);
                    table.CheckConstraint("CK_UploadRecords_OneRecord", "(CASE WHEN [BusId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [TerminalId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ScheduledTripId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [RefUserId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_UploadRecords_BusTerminals_TerminalId",
                        column: x => x.TerminalId,
                        principalTable: "BusTerminals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadRecords_Buses_BusId",
                        column: x => x.BusId,
                        principalTable: "Buses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadRecords_RefUsers_RefUserId",
                        column: x => x.RefUserId,
                        principalTable: "RefUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadRecords_ScheduledTrips_ScheduledTripId",
                        column: x => x.ScheduledTripId,
                        principalTable: "ScheduledTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadRecords_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeUploadTypes",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("a11d0000-0000-4000-8000-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Buses" },
                    { new Guid("a11d0000-0000-4000-8000-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Terminals" },
                    { new Guid("a11d0000-0000-4000-8000-000000000003"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ScheduledTrips" },
                    { new Guid("a11d0000-0000-4000-8000-000000000004"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 18, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Users" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusLogs_BusId_DateTimeCreated",
                table: "BusLogs",
                columns: new[] { "BusId", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_BusLogs_CreatedBy",
                table: "BusLogs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_BusLogs_UploadId",
                table: "BusLogs",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeUploadTypes_CreatedBy",
                table: "CodeDecodeUploadTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeUploadTypes_Decode",
                table: "CodeDecodeUploadTypes",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTripLogs_CreatedBy",
                table: "ScheduledTripLogs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTripLogs_ScheduledTripId_DateTimeCreated",
                table: "ScheduledTripLogs",
                columns: new[] { "ScheduledTripId", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTripLogs_UploadId",
                table: "ScheduledTripLogs",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalLogs_CreatedBy",
                table: "TerminalLogs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TerminalLogs_TerminalId_DateTimeCreated",
                table: "TerminalLogs",
                columns: new[] { "TerminalId", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_TerminalLogs_UploadId",
                table: "TerminalLogs",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_BusId",
                table: "UploadRecords",
                column: "BusId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_RefUserId",
                table: "UploadRecords",
                column: "RefUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_ScheduledTripId",
                table: "UploadRecords",
                column: "ScheduledTripId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_TerminalId",
                table: "UploadRecords",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_UploadId",
                table: "UploadRecords",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_UploadId_BusId",
                table: "UploadRecords",
                columns: new[] { "UploadId", "BusId" },
                unique: true,
                filter: "[BusId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_UploadId_RefUserId",
                table: "UploadRecords",
                columns: new[] { "UploadId", "RefUserId" },
                unique: true,
                filter: "[RefUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_UploadId_ScheduledTripId",
                table: "UploadRecords",
                columns: new[] { "UploadId", "ScheduledTripId" },
                unique: true,
                filter: "[ScheduledTripId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UploadRecords_UploadId_TerminalId",
                table: "UploadRecords",
                columns: new[] { "UploadId", "TerminalId" },
                unique: true,
                filter: "[TerminalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_CreatedBy_DateTimeCreated",
                table: "Uploads",
                columns: new[] { "CreatedBy", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_Uploads_UploadType",
                table: "Uploads",
                column: "UploadType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusLogs");

            migrationBuilder.DropTable(
                name: "ScheduledTripLogs");

            migrationBuilder.DropTable(
                name: "TerminalLogs");

            migrationBuilder.DropTable(
                name: "UploadRecords");

            migrationBuilder.DropTable(
                name: "Uploads");

            migrationBuilder.DropTable(
                name: "CodeDecodeUploadTypes");
        }
    }
}
