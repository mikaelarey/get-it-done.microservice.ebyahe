using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCodeDecodeAndAuditUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTrips_Status_DepartureAtUtc_BusRouteId",
                table: "ScheduledTrips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScheduledTrips_Status",
                table: "ScheduledTrips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Partners_Status",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_PartnerMemberRequests_PartnerId_Email",
                table: "PartnerMemberRequests");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_Status_SubmittedAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "UX_PartnerApplications_ActiveCreator",
                table: "PartnerApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplications_Status",
                table: "PartnerApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationHistories_Status",
                table: "PartnerApplicationHistories");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationApprovals_Status_DateTimeCreated",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationApprovals_PreviousStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationApprovals_Status",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropIndex(
                name: "IX_Outgoing_MessageId_AttemptCount_Status",
                table: "Outgoing");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Outgoing_Status",
                table: "Outgoing");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttemptDateTime_CreatedDateTime",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OutboxMessages_Status",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IncomingLogs_Status",
                table: "IncomingLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CodeDecodePaymentTypes",
                table: "CodeDecodePaymentTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CodeDecodePaymentStatuses",
                table: "CodeDecodePaymentStatuses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CodeDecodeBookingStatuses",
                table: "CodeDecodeBookingStatuses");

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

            migrationBuilder.Sql("""
                SELECT [Name] INTO [#LegacyCodeDecodeBookingStatuses] FROM [CodeDecodeBookingStatuses];
                DROP TABLE [CodeDecodeBookingStatuses];
                """);

            migrationBuilder.Sql("""
                SELECT [Name] INTO [#LegacyCodeDecodePaymentStatuses] FROM [CodeDecodePaymentStatuses];
                DROP TABLE [CodeDecodePaymentStatuses];
                """);

            migrationBuilder.Sql("""
                SELECT [Name] INTO [#LegacyCodeDecodePaymentTypes] FROM [CodeDecodePaymentTypes];
                DROP TABLE [CodeDecodePaymentTypes];
                """);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeScheduledTripStatus",
                table: "ScheduledTrips",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePartnerStatus",
                table: "Partners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePartnerMemberRequestStatus",
                table: "PartnerMemberRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePartnerApplicationStatus",
                table: "PartnerApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePartnerApplicationStatus",
                table: "PartnerApplicationHistories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePreviousPartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeOutgoingStatus",
                table: "Outgoing",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeOutboxStatus",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeIncomingStatus",
                table: "IncomingLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeIncomingStatus",
                table: "Incoming",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBookingStatus",
                table: "BusBookingStatusLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePaymentStatus",
                table: "BusBookingStatusLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePaymentType",
                table: "BusBookingStatusLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePreviousBookingStatus",
                table: "BusBookingStatusLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePreviousPaymentStatus",
                table: "BusBookingStatusLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBookingStatus",
                table: "BusBookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePaymentStatus",
                table: "BusBookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodePaymentType",
                table: "BusBookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RefUsers_ExternalUserId",
                table: "RefUsers",
                column: "ExternalUserId");

            migrationBuilder.CreateTable(
                name: "CodeDecodeIncomingStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeIncomingStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeIncomingStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeIncomingStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeOutboxStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeOutboxStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeOutboxStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeOutboxStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeOutgoingStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeOutgoingStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeOutgoingStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeOutgoingStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodePartnerApplicationApprovalStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodePartnerApplicationApprovalStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodePartnerApplicationApprovalStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodePartnerApplicationApprovalStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodePartnerApplicationStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodePartnerApplicationStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodePartnerApplicationStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodePartnerApplicationStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodePartnerMemberRequestStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodePartnerMemberRequestStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodePartnerMemberRequestStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodePartnerMemberRequestStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodePartnerStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodePartnerStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodePartnerStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodePartnerStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeScheduledTripStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeScheduledTripStatuses", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeScheduledTripStatuses_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeScheduledTripStatuses_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                CREATE TABLE [CodeDecodeBookingStatuses] (
                  [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_CodeDecodeBookingStatuses] PRIMARY KEY,
                  [Code] int IDENTITY(1,1) NOT NULL CONSTRAINT [AK_CodeDecodeBookingStatuses_Code] UNIQUE,
                  [Decode] varchar(64) NOT NULL,
                  [CreatedBy] nvarchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT [DF_CodeDecodeBookingStatuses_CreatedBy] DEFAULT N'System',
                  [DateTimeCreated] datetimeoffset NOT NULL CONSTRAINT [DF_CodeDecodeBookingStatuses_DateTimeCreated] DEFAULT SYSUTCDATETIME(),
                  CONSTRAINT [FK_CodeDecodeBookingStatuses_RefUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [RefUsers] ([ExternalUserId])
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE [CodeDecodePaymentStatuses] (
                  [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_CodeDecodePaymentStatuses] PRIMARY KEY,
                  [Code] int IDENTITY(1,1) NOT NULL CONSTRAINT [AK_CodeDecodePaymentStatuses_Code] UNIQUE,
                  [Decode] varchar(64) NOT NULL,
                  [CreatedBy] nvarchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT [DF_CodeDecodePaymentStatuses_CreatedBy] DEFAULT N'System',
                  [DateTimeCreated] datetimeoffset NOT NULL CONSTRAINT [DF_CodeDecodePaymentStatuses_DateTimeCreated] DEFAULT SYSUTCDATETIME(),
                  CONSTRAINT [FK_CodeDecodePaymentStatuses_RefUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [RefUsers] ([ExternalUserId])
                );
                """);

            migrationBuilder.Sql("""
                CREATE TABLE [CodeDecodePaymentTypes] (
                  [Id] uniqueidentifier NOT NULL CONSTRAINT [PK_CodeDecodePaymentTypes] PRIMARY KEY,
                  [Code] int IDENTITY(1,1) NOT NULL CONSTRAINT [AK_CodeDecodePaymentTypes_Code] UNIQUE,
                  [Decode] varchar(64) NOT NULL,
                  [CreatedBy] nvarchar(255) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT [DF_CodeDecodePaymentTypes_CreatedBy] DEFAULT N'System',
                  [DateTimeCreated] datetimeoffset NOT NULL CONSTRAINT [DF_CodeDecodePaymentTypes_DateTimeCreated] DEFAULT SYSUTCDATETIME(),
                  CONSTRAINT [FK_CodeDecodePaymentTypes_RefUsers_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [RefUsers] ([ExternalUserId])
                );
                """);

            migrationBuilder.InsertData(
                table: "RefUsers",
                columns: new[] { "Id", "CreatedAtUtc", "Email", "ExternalUserId", "FirstName", "LastName", "Provider", "Role" },
                values: new object[] { new Guid("00000000-0000-4000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system@internal.getitdone.invalid", "System", "System", "", "System", "Normal" });

            migrationBuilder.InsertData(
                table: "CodeDecodeBookingStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("476bcf03-0545-441d-8979-06829f5731bc"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cancelled" },
                    { new Guid("69de9984-dba6-493d-8e65-241a240dfdcf"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Submitted" },
                    { new Guid("8879fc68-3c13-44ce-874f-4875987bcca5"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Confirmed" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeIncomingStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("408529bf-8eed-45e6-870b-c262281e619a"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Processing" },
                    { new Guid("575db140-d2ae-4093-807f-8b02de9d0232"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Received" },
                    { new Guid("a9472be5-9710-468c-86b4-bdd86ea083ff"), 6, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "AcknowledgementFailed" },
                    { new Guid("af81901c-51fa-4e22-8fa7-c383d0433aec"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Processed" },
                    { new Guid("db3c2bb1-5fcd-40fc-840d-fa3198538fee"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Failed" },
                    { new Guid("f90fe4d3-1c6f-4a5b-8c39-7cd090d8a01e"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Acknowledged" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeOutboxStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("06423476-5b10-405e-8333-56d4dc82cd52"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Publishing" },
                    { new Guid("0afad4b3-0da3-4752-8e4e-0cac382dc8f0"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pending" },
                    { new Guid("9f599a8f-e086-4197-8481-1d7879e3097b"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Failed" },
                    { new Guid("da074c9f-db81-44e6-81e2-e6df1b58183b"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Published" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeOutgoingStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("48390a9b-7148-496f-831d-3c12d3efc534"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Publishing" },
                    { new Guid("4aa7bd9f-7e04-4121-8c73-57d4c9978707"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "LeaseExpired" },
                    { new Guid("62d50054-008f-4918-8eff-9010bcdbf152"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Failed" },
                    { new Guid("8998740b-c0f6-4597-8c3c-529821f8291a"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pending" },
                    { new Guid("d017d6f7-cf95-4501-89db-f676ec40eb3e"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Published" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodePartnerApplicationApprovalStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("06bd8b55-41dc-47d7-89b5-4334dbc0b2e5"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "NeedsChanges" },
                    { new Guid("08b13ca9-5f7b-47b6-82cb-53a76140be30"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Rejected" },
                    { new Guid("3f1bed28-1627-47b6-8062-f22b423e0d07"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnderReview" },
                    { new Guid("8d7ef477-421d-4b8a-8d06-ee54d2efd88d"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { new Guid("caa490f5-961e-4bda-83bc-25dff02361e0"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pending" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodePartnerApplicationStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("1e9d93f4-44d8-4b81-83c4-3b1c4e7fff7c"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Rejected" },
                    { new Guid("b5f8ce0b-69d6-40c1-8a1d-14f732468113"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { new Guid("b8a2faa4-467b-4a83-8716-0dc064db4fa0"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnderReview" },
                    { new Guid("dee48a86-4cc9-427c-8611-b844ae188c55"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Submitted" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodePartnerMemberRequestStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("7b66b932-30a3-4bc7-832d-5139015a3573"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Completed" },
                    { new Guid("b81ce98a-7318-4480-8ccf-c8c695a18cb9"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pending" },
                    { new Guid("fdd38f03-f8ad-4429-8591-5a22a0db01f0"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cancelled" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodePartnerStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("9ee1730f-14e4-4375-8e37-15c51ebddb76"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Approved" },
                    { new Guid("c39b77bc-0ca4-41ee-8254-e287222a1ed0"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Suspended" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodePaymentStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("1af56b0d-d074-4442-8a66-432ea0514344"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cancelled" },
                    { new Guid("46487216-b2b0-4f86-811a-eaa092ae3fe1"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Payment Success" },
                    { new Guid("5fee9e3e-7a19-40be-844e-34c094ed0fa5"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Pending Payment" },
                    { new Guid("9dd82075-f3cf-44bb-85da-4eaceba65243"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Payment Failed" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodePaymentTypes",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("595e2d36-ed5e-4d91-8a2d-ca1f0afc2d81"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "GCash" },
                    { new Guid("743539c8-8ead-40ec-8400-8af393cadb3e"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Maya" },
                    { new Guid("ad3f2478-9226-4dca-8f14-536c3af1b857"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Credit/Debit Card" },
                    { new Guid("b77c0f51-0a7e-46d7-8332-7b158af304b1"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cash" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeScheduledTripStatuses",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("d214bcab-649b-4c8c-8776-a450db4d0816"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Cancelled" },
                    { new Guid("ecde4545-b6ce-4c39-835d-f90b898fcfd2"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Scheduled" }
                });

            migrationBuilder.Sql("""
                INSERT INTO [CodeDecodeBookingStatuses] ([Id], [Decode])
                SELECT NEWID(), old.[Name] FROM [#LegacyCodeDecodeBookingStatuses] old
                WHERE NOT EXISTS (SELECT 1 FROM [CodeDecodeBookingStatuses] currentSetup WHERE currentSetup.[Decode] = old.[Name]);
                DROP TABLE [#LegacyCodeDecodeBookingStatuses];
                """);

            migrationBuilder.Sql("""
                INSERT INTO [CodeDecodePaymentStatuses] ([Id], [Decode])
                SELECT NEWID(), old.[Name] FROM [#LegacyCodeDecodePaymentStatuses] old
                WHERE NOT EXISTS (SELECT 1 FROM [CodeDecodePaymentStatuses] currentSetup WHERE currentSetup.[Decode] = old.[Name]);
                DROP TABLE [#LegacyCodeDecodePaymentStatuses];
                """);

            migrationBuilder.Sql("""
                INSERT INTO [CodeDecodePaymentTypes] ([Id], [Decode])
                SELECT NEWID(), old.[Name] FROM [#LegacyCodeDecodePaymentTypes] old
                WHERE NOT EXISTS (SELECT 1 FROM [CodeDecodePaymentTypes] currentSetup WHERE currentSetup.[Decode] = old.[Name]);
                DROP TABLE [#LegacyCodeDecodePaymentTypes];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookingStatusLogs] source LEFT JOIN [CodeDecodeBookingStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookingStatusLogs.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeBookingStatus] = setup.[Code]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodeBookingStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookingStatusLogs] source LEFT JOIN [CodeDecodeBookingStatuses] setup ON setup.[Decode] = source.[PreviousStatus] WHERE source.[PreviousStatus] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookingStatusLogs.PreviousStatus; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePreviousBookingStatus] = setup.[Code]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodeBookingStatuses] setup ON setup.[Decode] = source.[PreviousStatus];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookingStatusLogs] source LEFT JOIN [CodeDecodePaymentStatuses] setup ON setup.[Decode] = source.[PaymentStatus] WHERE source.[PaymentStatus] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookingStatusLogs.PaymentStatus; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePaymentStatus] = setup.[Code]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodePaymentStatuses] setup ON setup.[Decode] = source.[PaymentStatus];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookingStatusLogs] source LEFT JOIN [CodeDecodePaymentStatuses] setup ON setup.[Decode] = source.[PreviousPaymentStatus] WHERE source.[PreviousPaymentStatus] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookingStatusLogs.PreviousPaymentStatus; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePreviousPaymentStatus] = setup.[Code]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodePaymentStatuses] setup ON setup.[Decode] = source.[PreviousPaymentStatus];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookingStatusLogs] source LEFT JOIN [CodeDecodePaymentTypes] setup ON setup.[Decode] = source.[PaymentType] WHERE source.[PaymentType] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookingStatusLogs.PaymentType; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePaymentType] = setup.[Code]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodePaymentTypes] setup ON setup.[Decode] = source.[PaymentType];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookings] source LEFT JOIN [CodeDecodeBookingStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookings.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeBookingStatus] = setup.[Code]
                FROM [BusBookings] source JOIN [CodeDecodeBookingStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookings] source LEFT JOIN [CodeDecodePaymentStatuses] setup ON setup.[Decode] = source.[PaymentStatus] WHERE source.[PaymentStatus] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookings.PaymentStatus; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePaymentStatus] = setup.[Code]
                FROM [BusBookings] source JOIN [CodeDecodePaymentStatuses] setup ON setup.[Decode] = source.[PaymentStatus];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [BusBookings] source LEFT JOIN [CodeDecodePaymentTypes] setup ON setup.[Decode] = source.[PaymentType] WHERE source.[PaymentType] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped BusBookings.PaymentType; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePaymentType] = setup.[Code]
                FROM [BusBookings] source JOIN [CodeDecodePaymentTypes] setup ON setup.[Decode] = source.[PaymentType];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [Partners] source LEFT JOIN [CodeDecodePartnerStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped Partners.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePartnerStatus] = setup.[Code]
                FROM [Partners] source JOIN [CodeDecodePartnerStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [ScheduledTrips] source LEFT JOIN [CodeDecodeScheduledTripStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped ScheduledTrips.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeScheduledTripStatus] = setup.[Code]
                FROM [ScheduledTrips] source JOIN [CodeDecodeScheduledTripStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [Incoming] source LEFT JOIN [CodeDecodeIncomingStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped Incoming.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeIncomingStatus] = setup.[Code]
                FROM [Incoming] source JOIN [CodeDecodeIncomingStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [IncomingLogs] source LEFT JOIN [CodeDecodeIncomingStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped IncomingLogs.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeIncomingStatus] = setup.[Code]
                FROM [IncomingLogs] source JOIN [CodeDecodeIncomingStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [OutboxMessages] source LEFT JOIN [CodeDecodeOutboxStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped OutboxMessages.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeOutboxStatus] = setup.[Code]
                FROM [OutboxMessages] source JOIN [CodeDecodeOutboxStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [Outgoing] source LEFT JOIN [CodeDecodeOutgoingStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped Outgoing.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodeOutgoingStatus] = setup.[Code]
                FROM [Outgoing] source JOIN [CodeDecodeOutgoingStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [PartnerApplications] source LEFT JOIN [CodeDecodePartnerApplicationStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped PartnerApplications.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePartnerApplicationStatus] = setup.[Code]
                FROM [PartnerApplications] source JOIN [CodeDecodePartnerApplicationStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [PartnerApplicationApprovals] source LEFT JOIN [CodeDecodePartnerApplicationApprovalStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped PartnerApplicationApprovals.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePartnerApplicationApprovalStatus] = setup.[Code]
                FROM [PartnerApplicationApprovals] source JOIN [CodeDecodePartnerApplicationApprovalStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [PartnerApplicationApprovals] source LEFT JOIN [CodeDecodePartnerApplicationApprovalStatuses] setup ON setup.[Decode] = source.[PreviousStatus] WHERE source.[PreviousStatus] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped PartnerApplicationApprovals.PreviousStatus; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePreviousPartnerApplicationApprovalStatus] = setup.[Code]
                FROM [PartnerApplicationApprovals] source JOIN [CodeDecodePartnerApplicationApprovalStatuses] setup ON setup.[Decode] = source.[PreviousStatus];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [PartnerApplicationHistories] source LEFT JOIN [CodeDecodePartnerApplicationStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped PartnerApplicationHistories.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePartnerApplicationStatus] = setup.[Code]
                FROM [PartnerApplicationHistories] source JOIN [CodeDecodePartnerApplicationStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.Sql("""
                -- Preserve the stored label; never substitute a default for an unknown status.
                IF EXISTS (SELECT 1 FROM [PartnerMemberRequests] source LEFT JOIN [CodeDecodePartnerMemberRequestStatuses] setup ON setup.[Decode] = source.[Status] WHERE source.[Status] IS NOT NULL AND setup.[Code] IS NULL)
                  THROW 51000, 'Unmapped PartnerMemberRequests.Status; register the setup value before migrating.', 1;
                UPDATE source SET [CodeDecodePartnerMemberRequestStatus] = setup.[Code]
                FROM [PartnerMemberRequests] source JOIN [CodeDecodePartnerMemberRequestStatuses] setup ON setup.[Decode] = source.[Status];
                """);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ScheduledTrips");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PartnerMemberRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PartnerApplicationHistories");

            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Outgoing");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IncomingLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Incoming");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "PreviousPaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "PaymentType",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BusBookings");

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [BusBookings] actor LEFT JOIN [RefUsers] u ON u.[ExternalUserId] = actor.[CreatedBy] WHERE actor.[CreatedBy] IS NOT NULL AND u.[ExternalUserId] IS NULL)
                  THROW 51001, 'Unknown audit user in BusBookings.CreatedBy; synchronize registered users into RefUsers before migrating.', 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [CodeDecodeBookingStatuses] actor LEFT JOIN [RefUsers] u ON u.[ExternalUserId] = actor.[CreatedBy] WHERE actor.[CreatedBy] IS NOT NULL AND u.[ExternalUserId] IS NULL)
                  THROW 51001, 'Unknown audit user in CodeDecodeBookingStatuses.CreatedBy; synchronize registered users into RefUsers before migrating.', 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [CodeDecodePaymentStatuses] actor LEFT JOIN [RefUsers] u ON u.[ExternalUserId] = actor.[CreatedBy] WHERE actor.[CreatedBy] IS NOT NULL AND u.[ExternalUserId] IS NULL)
                  THROW 51001, 'Unknown audit user in CodeDecodePaymentStatuses.CreatedBy; synchronize registered users into RefUsers before migrating.', 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [CodeDecodePaymentTypes] actor LEFT JOIN [RefUsers] u ON u.[ExternalUserId] = actor.[CreatedBy] WHERE actor.[CreatedBy] IS NOT NULL AND u.[ExternalUserId] IS NULL)
                  THROW 51001, 'Unknown audit user in CodeDecodePaymentTypes.CreatedBy; synchronize registered users into RefUsers before migrating.', 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplicationApprovals] actor LEFT JOIN [RefUsers] u ON u.[ExternalUserId] = actor.[ChangedBy] WHERE actor.[ChangedBy] IS NOT NULL AND u.[ExternalUserId] IS NULL)
                  THROW 51001, 'Unknown audit user in PartnerApplicationApprovals.ChangedBy; synchronize registered users into RefUsers before migrating.', 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplications] actor LEFT JOIN [RefUsers] u ON u.[ExternalUserId] = actor.[CreatedBy] WHERE actor.[CreatedBy] IS NOT NULL AND u.[ExternalUserId] IS NULL)
                  THROW 51001, 'Unknown audit user in PartnerApplications.CreatedBy; synchronize registered users into RefUsers before migrating.', 1;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerMemberRequests] actor LEFT JOIN [RefUsers] u ON u.[ExternalUserId] = actor.[CreatedBy] WHERE actor.[CreatedBy] IS NOT NULL AND u.[ExternalUserId] IS NULL)
                  THROW 51001, 'Unknown audit user in PartnerMemberRequests.CreatedBy; synchronize registered users into RefUsers before migrating.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTrips_CodeDecodeScheduledTripStatus_DepartureAtUtc_BusRouteId",
                table: "ScheduledTrips",
                columns: new[] { "CodeDecodeScheduledTripStatus", "DepartureAtUtc", "BusRouteId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScheduledTrips_Status",
                table: "ScheduledTrips",
                sql: "[CodeDecodeScheduledTripStatus] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_CodeDecodePartnerStatus",
                table: "Partners",
                column: "CodeDecodePartnerStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Partners_Status",
                table: "Partners",
                sql: "[CodeDecodePartnerStatus] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerMemberRequests_CodeDecodePartnerMemberRequestStatus",
                table: "PartnerMemberRequests",
                column: "CodeDecodePartnerMemberRequestStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerMemberRequests_CreatedBy",
                table: "PartnerMemberRequests",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerMemberRequests_PartnerId_Email",
                table: "PartnerMemberRequests",
                columns: new[] { "PartnerId", "Email" },
                unique: true,
                filter: "[CodeDecodePartnerMemberRequestStatus] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodePartnerApplicationStatus_SubmittedAtUtc",
                table: "PartnerApplications",
                columns: new[] { "CodeDecodePartnerApplicationStatus", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PartnerApplications_ActiveCreator",
                table: "PartnerApplications",
                column: "CreatedBy",
                unique: true,
                filter: "[CreatedBy] IS NOT NULL AND [CodeDecodePartnerApplicationStatus] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplications_Status",
                table: "PartnerApplications",
                sql: "[CodeDecodePartnerApplicationStatus] IN (1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationHistories_CodeDecodePartnerApplicationStatus",
                table: "PartnerApplicationHistories",
                column: "CodeDecodePartnerApplicationStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationHistories_Status",
                table: "PartnerApplicationHistories",
                sql: "[CodeDecodePartnerApplicationStatus] IN (1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationApprovals_ChangedBy",
                table: "PartnerApplicationApprovals",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationApprovals_CodeDecodePartnerApplicationApprovalStatus_DateTimeCreated",
                table: "PartnerApplicationApprovals",
                columns: new[] { "CodeDecodePartnerApplicationApprovalStatus", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationApprovals_CodeDecodePreviousPartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals",
                column: "CodeDecodePreviousPartnerApplicationApprovalStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationApprovals_PreviousStatus",
                table: "PartnerApplicationApprovals",
                sql: "[CodeDecodePreviousPartnerApplicationApprovalStatus] IS NULL OR [CodeDecodePreviousPartnerApplicationApprovalStatus] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationApprovals_Status",
                table: "PartnerApplicationApprovals",
                sql: "[CodeDecodePartnerApplicationApprovalStatus] IN (1, 2, 3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_CodeDecodeOutgoingStatus",
                table: "Outgoing",
                column: "CodeDecodeOutgoingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_MessageId_AttemptCount_CodeDecodeOutgoingStatus",
                table: "Outgoing",
                columns: new[] { "MessageId", "AttemptCount", "CodeDecodeOutgoingStatus" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Outgoing_Status",
                table: "Outgoing",
                sql: "[CodeDecodeOutgoingStatus] IN (1, 2, 3, 4, 5)");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CodeDecodeOutboxStatus_NextAttemptDateTime_CreatedDateTime",
                table: "OutboxMessages",
                columns: new[] { "CodeDecodeOutboxStatus", "NextAttemptDateTime", "CreatedDateTime" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OutboxMessages_Status",
                table: "OutboxMessages",
                sql: "[CodeDecodeOutboxStatus] IN (1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingLogs_CodeDecodeIncomingStatus",
                table: "IncomingLogs",
                column: "CodeDecodeIncomingStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IncomingLogs_Status",
                table: "IncomingLogs",
                sql: "[CodeDecodeIncomingStatus] IN (1, 2, 3, 4, 5, 6)");

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_CodeDecodeIncomingStatus",
                table: "Incoming",
                column: "CodeDecodeIncomingStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming",
                sql: "[CodeDecodeIncomingStatus] IN (1, 2, 3, 4)");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePaymentTypes_CreatedBy",
                table: "CodeDecodePaymentTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePaymentTypes_Decode",
                table: "CodeDecodePaymentTypes",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePaymentStatuses_CreatedBy",
                table: "CodeDecodePaymentStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePaymentStatuses_Decode",
                table: "CodeDecodePaymentStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBookingStatuses_CreatedBy",
                table: "CodeDecodeBookingStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBookingStatuses_Decode",
                table: "CodeDecodeBookingStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodeBookingStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodeBookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePaymentStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePaymentType",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePaymentType");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePreviousBookingStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePreviousBookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePreviousPaymentStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePreviousPaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_CodeDecodeBookingStatus",
                table: "BusBookings",
                column: "CodeDecodeBookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_CodeDecodePaymentStatus",
                table: "BusBookings",
                column: "CodeDecodePaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_BusBookings_CodeDecodePaymentType",
                table: "BusBookings",
                column: "CodeDecodePaymentType");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_CancelledPayment",
                table: "BusBookings",
                sql: "([CodeDecodeBookingStatus] = 3 AND [CodeDecodePaymentStatus] = 4) OR ([CodeDecodeBookingStatus] <> 3 AND [CodeDecodePaymentStatus] <> 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_ConfirmedPayment",
                table: "BusBookings",
                sql: "[CodeDecodeBookingStatus] <> 2 OR ([CodeDecodePaymentStatus] = 3 AND [ReservedSeats] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_ReservedSeats",
                table: "BusBookings",
                sql: "[ReservedSeats] BETWEEN 0 AND 20 AND ([ReservedSeats] = 0 OR ([ScheduledTripId] IS NOT NULL AND [CodeDecodeBookingStatus] = 2 AND [CodeDecodePaymentStatus] = 3))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BusBookings_Status",
                table: "BusBookings",
                sql: "[CodeDecodeBookingStatus] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeIncomingStatuses_CreatedBy",
                table: "CodeDecodeIncomingStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeIncomingStatuses_Decode",
                table: "CodeDecodeIncomingStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeOutboxStatuses_CreatedBy",
                table: "CodeDecodeOutboxStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeOutboxStatuses_Decode",
                table: "CodeDecodeOutboxStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeOutgoingStatuses_CreatedBy",
                table: "CodeDecodeOutgoingStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeOutgoingStatuses_Decode",
                table: "CodeDecodeOutgoingStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerApplicationApprovalStatuses_CreatedBy",
                table: "CodeDecodePartnerApplicationApprovalStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerApplicationApprovalStatuses_Decode",
                table: "CodeDecodePartnerApplicationApprovalStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerApplicationStatuses_CreatedBy",
                table: "CodeDecodePartnerApplicationStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerApplicationStatuses_Decode",
                table: "CodeDecodePartnerApplicationStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerMemberRequestStatuses_CreatedBy",
                table: "CodeDecodePartnerMemberRequestStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerMemberRequestStatuses_Decode",
                table: "CodeDecodePartnerMemberRequestStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerStatuses_CreatedBy",
                table: "CodeDecodePartnerStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodePartnerStatuses_Decode",
                table: "CodeDecodePartnerStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeScheduledTripStatuses_CreatedBy",
                table: "CodeDecodeScheduledTripStatuses",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeScheduledTripStatuses_Decode",
                table: "CodeDecodeScheduledTripStatuses",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_CodeDecodeBookingStatuses_CodeDecodeBookingStatus",
                table: "BusBookings",
                column: "CodeDecodeBookingStatus",
                principalTable: "CodeDecodeBookingStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentStatuses_CodeDecodePaymentStatus",
                table: "BusBookings",
                column: "CodeDecodePaymentStatus",
                principalTable: "CodeDecodePaymentStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentTypes_CodeDecodePaymentType",
                table: "BusBookings",
                column: "CodeDecodePaymentType",
                principalTable: "CodeDecodePaymentTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookings_RefUsers_CreatedBy",
                table: "BusBookings",
                column: "CreatedBy",
                principalTable: "RefUsers",
                principalColumn: "ExternalUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodeBookingStatuses_CodeDecodeBookingStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodeBookingStatus",
                principalTable: "CodeDecodeBookingStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodeBookingStatuses_CodeDecodePreviousBookingStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePreviousBookingStatus",
                principalTable: "CodeDecodeBookingStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodePaymentStatuses_CodeDecodePaymentStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePaymentStatus",
                principalTable: "CodeDecodePaymentStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodePaymentStatuses_CodeDecodePreviousPaymentStatus",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePreviousPaymentStatus",
                principalTable: "CodeDecodePaymentStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodePaymentTypes_CodeDecodePaymentType",
                table: "BusBookingStatusLogs",
                column: "CodeDecodePaymentType",
                principalTable: "CodeDecodePaymentTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);







            migrationBuilder.AddForeignKey(
                name: "FK_Incoming_CodeDecodeIncomingStatuses_CodeDecodeIncomingStatus",
                table: "Incoming",
                column: "CodeDecodeIncomingStatus",
                principalTable: "CodeDecodeIncomingStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IncomingLogs_CodeDecodeIncomingStatuses_CodeDecodeIncomingStatus",
                table: "IncomingLogs",
                column: "CodeDecodeIncomingStatus",
                principalTable: "CodeDecodeIncomingStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxMessages_CodeDecodeOutboxStatuses_CodeDecodeOutboxStatus",
                table: "OutboxMessages",
                column: "CodeDecodeOutboxStatus",
                principalTable: "CodeDecodeOutboxStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Outgoing_CodeDecodeOutgoingStatuses_CodeDecodeOutgoingStatus",
                table: "Outgoing",
                column: "CodeDecodeOutgoingStatus",
                principalTable: "CodeDecodeOutgoingStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplicationApprovals_CodeDecodePartnerApplicationApprovalStatuses_CodeDecodePartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals",
                column: "CodeDecodePartnerApplicationApprovalStatus",
                principalTable: "CodeDecodePartnerApplicationApprovalStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplicationApprovals_CodeDecodePartnerApplicationApprovalStatuses_CodeDecodePreviousPartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals",
                column: "CodeDecodePreviousPartnerApplicationApprovalStatus",
                principalTable: "CodeDecodePartnerApplicationApprovalStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplicationApprovals_RefUsers_ChangedBy",
                table: "PartnerApplicationApprovals",
                column: "ChangedBy",
                principalTable: "RefUsers",
                principalColumn: "ExternalUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplicationHistories_CodeDecodePartnerApplicationStatuses_CodeDecodePartnerApplicationStatus",
                table: "PartnerApplicationHistories",
                column: "CodeDecodePartnerApplicationStatus",
                principalTable: "CodeDecodePartnerApplicationStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodePartnerApplicationStatuses_CodeDecodePartnerApplicationStatus",
                table: "PartnerApplications",
                column: "CodeDecodePartnerApplicationStatus",
                principalTable: "CodeDecodePartnerApplicationStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_RefUsers_CreatedBy",
                table: "PartnerApplications",
                column: "CreatedBy",
                principalTable: "RefUsers",
                principalColumn: "ExternalUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerMemberRequests_CodeDecodePartnerMemberRequestStatuses_CodeDecodePartnerMemberRequestStatus",
                table: "PartnerMemberRequests",
                column: "CodeDecodePartnerMemberRequestStatus",
                principalTable: "CodeDecodePartnerMemberRequestStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerMemberRequests_RefUsers_CreatedBy",
                table: "PartnerMemberRequests",
                column: "CreatedBy",
                principalTable: "RefUsers",
                principalColumn: "ExternalUserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Partners_CodeDecodePartnerStatuses_CodeDecodePartnerStatus",
                table: "Partners",
                column: "CodeDecodePartnerStatus",
                principalTable: "CodeDecodePartnerStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledTrips_CodeDecodeScheduledTripStatuses_CodeDecodeScheduledTripStatus",
                table: "ScheduledTrips",
                column: "CodeDecodeScheduledTripStatus",
                principalTable: "CodeDecodeScheduledTripStatuses",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_CodeDecodeBookingStatuses_CodeDecodeBookingStatus",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentStatuses_CodeDecodePaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_CodeDecodePaymentTypes_CodeDecodePaymentType",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookings_RefUsers_CreatedBy",
                table: "BusBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodeBookingStatuses_CodeDecodeBookingStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodeBookingStatuses_CodeDecodePreviousBookingStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodePaymentStatuses_CodeDecodePaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodePaymentStatuses_CodeDecodePreviousPaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BusBookingStatusLogs_CodeDecodePaymentTypes_CodeDecodePaymentType",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Incoming_CodeDecodeIncomingStatuses_CodeDecodeIncomingStatus",
                table: "Incoming");

            migrationBuilder.DropForeignKey(
                name: "FK_IncomingLogs_CodeDecodeIncomingStatuses_CodeDecodeIncomingStatus",
                table: "IncomingLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboxMessages_CodeDecodeOutboxStatuses_CodeDecodeOutboxStatus",
                table: "OutboxMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Outgoing_CodeDecodeOutgoingStatuses_CodeDecodeOutgoingStatus",
                table: "Outgoing");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplicationApprovals_CodeDecodePartnerApplicationApprovalStatuses_CodeDecodePartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplicationApprovals_CodeDecodePartnerApplicationApprovalStatuses_CodeDecodePreviousPartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplicationApprovals_RefUsers_ChangedBy",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplicationHistories_CodeDecodePartnerApplicationStatuses_CodeDecodePartnerApplicationStatus",
                table: "PartnerApplicationHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodePartnerApplicationStatuses_CodeDecodePartnerApplicationStatus",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_RefUsers_CreatedBy",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerMemberRequests_CodeDecodePartnerMemberRequestStatuses_CodeDecodePartnerMemberRequestStatus",
                table: "PartnerMemberRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerMemberRequests_RefUsers_CreatedBy",
                table: "PartnerMemberRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Partners_CodeDecodePartnerStatuses_CodeDecodePartnerStatus",
                table: "Partners");

            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledTrips_CodeDecodeScheduledTripStatuses_CodeDecodeScheduledTripStatus",
                table: "ScheduledTrips");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTrips_CodeDecodeScheduledTripStatus_DepartureAtUtc_BusRouteId",
                table: "ScheduledTrips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ScheduledTrips_Status",
                table: "ScheduledTrips");



            migrationBuilder.DropIndex(
                name: "IX_Partners_CodeDecodePartnerStatus",
                table: "Partners");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Partners_Status",
                table: "Partners");

            migrationBuilder.DropIndex(
                name: "IX_PartnerMemberRequests_CodeDecodePartnerMemberRequestStatus",
                table: "PartnerMemberRequests");

            migrationBuilder.DropIndex(
                name: "IX_PartnerMemberRequests_CreatedBy",
                table: "PartnerMemberRequests");

            migrationBuilder.DropIndex(
                name: "IX_PartnerMemberRequests_PartnerId_Email",
                table: "PartnerMemberRequests");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodePartnerApplicationStatus_SubmittedAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "UX_PartnerApplications_ActiveCreator",
                table: "PartnerApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplications_Status",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationHistories_CodeDecodePartnerApplicationStatus",
                table: "PartnerApplicationHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationHistories_Status",
                table: "PartnerApplicationHistories");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationApprovals_ChangedBy",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationApprovals_CodeDecodePartnerApplicationApprovalStatus_DateTimeCreated",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplicationApprovals_CodeDecodePreviousPartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationApprovals_PreviousStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationApprovals_Status",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropIndex(
                name: "IX_Outgoing_CodeDecodeOutgoingStatus",
                table: "Outgoing");

            migrationBuilder.DropIndex(
                name: "IX_Outgoing_MessageId_AttemptCount_CodeDecodeOutgoingStatus",
                table: "Outgoing");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Outgoing_Status",
                table: "Outgoing");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_CodeDecodeOutboxStatus_NextAttemptDateTime_CreatedDateTime",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OutboxMessages_Status",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_IncomingLogs_CodeDecodeIncomingStatus",
                table: "IncomingLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_IncomingLogs_Status",
                table: "IncomingLogs");

            migrationBuilder.DropIndex(
                name: "IX_Incoming_CodeDecodeIncomingStatus",
                table: "Incoming");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming");

            migrationBuilder.DropIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodeBookingStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePaymentType",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePreviousBookingStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_BusBookingStatusLogs_CodeDecodePreviousPaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_CodeDecodeBookingStatus",
                table: "BusBookings");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_CodeDecodePaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropIndex(
                name: "IX_BusBookings_CodeDecodePaymentType",
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

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ScheduledTrips",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Partners",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PartnerMemberRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PartnerApplications",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "Submitted");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PartnerApplicationHistories",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PreviousStatus",
                table: "PartnerApplicationApprovals",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PartnerApplicationApprovals",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Outgoing",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OutboxMessages",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "IncomingLogs",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Incoming",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "BusBookingStatusLogs",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentType",
                table: "BusBookingStatusLogs",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousPaymentStatus",
                table: "BusBookingStatusLogs",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousStatus",
                table: "BusBookingStatusLogs",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BusBookingStatusLogs",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

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

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BusBookings",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodeBookingStatuses] setup ON setup.[Code] = source.[CodeDecodeBookingStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [PreviousStatus] = setup.[Decode]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodeBookingStatuses] setup ON setup.[Code] = source.[CodeDecodePreviousBookingStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [PaymentStatus] = setup.[Decode]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodePaymentStatuses] setup ON setup.[Code] = source.[CodeDecodePaymentStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [PreviousPaymentStatus] = setup.[Decode]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodePaymentStatuses] setup ON setup.[Code] = source.[CodeDecodePreviousPaymentStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [PaymentType] = setup.[Decode]
                FROM [BusBookingStatusLogs] source JOIN [CodeDecodePaymentTypes] setup ON setup.[Code] = source.[CodeDecodePaymentType];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [BusBookings] source JOIN [CodeDecodeBookingStatuses] setup ON setup.[Code] = source.[CodeDecodeBookingStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [PaymentStatus] = setup.[Decode]
                FROM [BusBookings] source JOIN [CodeDecodePaymentStatuses] setup ON setup.[Code] = source.[CodeDecodePaymentStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [PaymentType] = setup.[Decode]
                FROM [BusBookings] source JOIN [CodeDecodePaymentTypes] setup ON setup.[Code] = source.[CodeDecodePaymentType];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [Partners] source JOIN [CodeDecodePartnerStatuses] setup ON setup.[Code] = source.[CodeDecodePartnerStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [ScheduledTrips] source JOIN [CodeDecodeScheduledTripStatuses] setup ON setup.[Code] = source.[CodeDecodeScheduledTripStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [Incoming] source JOIN [CodeDecodeIncomingStatuses] setup ON setup.[Code] = source.[CodeDecodeIncomingStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [IncomingLogs] source JOIN [CodeDecodeIncomingStatuses] setup ON setup.[Code] = source.[CodeDecodeIncomingStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [OutboxMessages] source JOIN [CodeDecodeOutboxStatuses] setup ON setup.[Code] = source.[CodeDecodeOutboxStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [Outgoing] source JOIN [CodeDecodeOutgoingStatuses] setup ON setup.[Code] = source.[CodeDecodeOutgoingStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [PartnerApplications] source JOIN [CodeDecodePartnerApplicationStatuses] setup ON setup.[Code] = source.[CodeDecodePartnerApplicationStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [PartnerApplicationApprovals] source JOIN [CodeDecodePartnerApplicationApprovalStatuses] setup ON setup.[Code] = source.[CodeDecodePartnerApplicationApprovalStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [PreviousStatus] = setup.[Decode]
                FROM [PartnerApplicationApprovals] source JOIN [CodeDecodePartnerApplicationApprovalStatuses] setup ON setup.[Code] = source.[CodeDecodePreviousPartnerApplicationApprovalStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [PartnerApplicationHistories] source JOIN [CodeDecodePartnerApplicationStatuses] setup ON setup.[Code] = source.[CodeDecodePartnerApplicationStatus];
                """);

            migrationBuilder.Sql("""
                UPDATE source SET [Status] = setup.[Decode]
                FROM [PartnerMemberRequests] source JOIN [CodeDecodePartnerMemberRequestStatuses] setup ON setup.[Code] = source.[CodeDecodePartnerMemberRequestStatus];
                """);

            migrationBuilder.DropColumn(
                name: "CodeDecodeScheduledTripStatus",
                table: "ScheduledTrips");

            migrationBuilder.DropColumn(
                name: "CodeDecodePartnerStatus",
                table: "Partners");

            migrationBuilder.DropColumn(
                name: "CodeDecodePartnerMemberRequestStatus",
                table: "PartnerMemberRequests");

            migrationBuilder.DropColumn(
                name: "CodeDecodePartnerApplicationStatus",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodePartnerApplicationStatus",
                table: "PartnerApplicationHistories");

            migrationBuilder.DropColumn(
                name: "CodeDecodePartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropColumn(
                name: "CodeDecodePreviousPartnerApplicationApprovalStatus",
                table: "PartnerApplicationApprovals");

            migrationBuilder.DropColumn(
                name: "CodeDecodeOutgoingStatus",
                table: "Outgoing");

            migrationBuilder.DropColumn(
                name: "CodeDecodeOutboxStatus",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "CodeDecodeIncomingStatus",
                table: "IncomingLogs");

            migrationBuilder.DropColumn(
                name: "CodeDecodeIncomingStatus",
                table: "Incoming");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBookingStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "CodeDecodePaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "CodeDecodePaymentType",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "CodeDecodePreviousBookingStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "CodeDecodePreviousPaymentStatus",
                table: "BusBookingStatusLogs");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBookingStatus",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "CodeDecodePaymentStatus",
                table: "BusBookings");

            migrationBuilder.DropColumn(
                name: "CodeDecodePaymentType",
                table: "BusBookings");

            migrationBuilder.Sql("""
                SELECT [Decode] INTO [#LegacyCodeDecodeBookingStatuses] FROM [CodeDecodeBookingStatuses];
                DROP TABLE [CodeDecodeBookingStatuses];
                CREATE TABLE [CodeDecodeBookingStatuses] ([Name] varchar(32) NOT NULL CONSTRAINT [PK_CodeDecodeBookingStatuses] PRIMARY KEY);
                INSERT INTO [CodeDecodeBookingStatuses] ([Name]) SELECT [Decode] FROM [#LegacyCodeDecodeBookingStatuses];
                DROP TABLE [#LegacyCodeDecodeBookingStatuses];
                """);

            migrationBuilder.Sql("""
                SELECT [Decode] INTO [#LegacyCodeDecodePaymentStatuses] FROM [CodeDecodePaymentStatuses];
                DROP TABLE [CodeDecodePaymentStatuses];
                CREATE TABLE [CodeDecodePaymentStatuses] ([Name] varchar(32) NOT NULL CONSTRAINT [PK_CodeDecodePaymentStatuses] PRIMARY KEY);
                INSERT INTO [CodeDecodePaymentStatuses] ([Name]) SELECT [Decode] FROM [#LegacyCodeDecodePaymentStatuses];
                DROP TABLE [#LegacyCodeDecodePaymentStatuses];
                """);

            migrationBuilder.Sql("""
                SELECT [Decode] INTO [#LegacyCodeDecodePaymentTypes] FROM [CodeDecodePaymentTypes];
                DROP TABLE [CodeDecodePaymentTypes];
                CREATE TABLE [CodeDecodePaymentTypes] ([Name] varchar(32) NOT NULL CONSTRAINT [PK_CodeDecodePaymentTypes] PRIMARY KEY);
                INSERT INTO [CodeDecodePaymentTypes] ([Name]) SELECT [Decode] FROM [#LegacyCodeDecodePaymentTypes];
                DROP TABLE [#LegacyCodeDecodePaymentTypes];
                """);

            migrationBuilder.DropTable(
                name: "CodeDecodeIncomingStatuses");

            migrationBuilder.DropTable(
                name: "CodeDecodeOutboxStatuses");

            migrationBuilder.DropTable(
                name: "CodeDecodeOutgoingStatuses");

            migrationBuilder.DropTable(
                name: "CodeDecodePartnerApplicationApprovalStatuses");

            migrationBuilder.DropTable(
                name: "CodeDecodePartnerApplicationStatuses");

            migrationBuilder.DropTable(
                name: "CodeDecodePartnerMemberRequestStatuses");

            migrationBuilder.DropTable(
                name: "CodeDecodePartnerStatuses");

            migrationBuilder.DropTable(
                name: "CodeDecodeScheduledTripStatuses");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_RefUsers_ExternalUserId",
                table: "RefUsers");

            migrationBuilder.DeleteData(
                table: "RefUsers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-4000-8000-000000000001"));

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTrips_Status_DepartureAtUtc_BusRouteId",
                table: "ScheduledTrips",
                columns: new[] { "Status", "DepartureAtUtc", "BusRouteId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ScheduledTrips_Status",
                table: "ScheduledTrips",
                sql: "[Status] IN ('Scheduled', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Partners_Status",
                table: "Partners",
                sql: "[Status] IN ('Approved', 'Suspended')");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerMemberRequests_PartnerId_Email",
                table: "PartnerMemberRequests",
                columns: new[] { "PartnerId", "Email" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_Status_SubmittedAtUtc",
                table: "PartnerApplications",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PartnerApplications_ActiveCreator",
                table: "PartnerApplications",
                column: "CreatedBy",
                unique: true,
                filter: "[CreatedBy] IS NOT NULL AND [Status] IN ('Submitted', 'UnderReview')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplications_Status",
                table: "PartnerApplications",
                sql: "[Status] IN ('Submitted', 'UnderReview', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationHistories_Status",
                table: "PartnerApplicationHistories",
                sql: "[Status] IN ('Submitted', 'UnderReview', 'Approved', 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationApprovals_Status_DateTimeCreated",
                table: "PartnerApplicationApprovals",
                columns: new[] { "Status", "DateTimeCreated" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationApprovals_PreviousStatus",
                table: "PartnerApplicationApprovals",
                sql: "[PreviousStatus] IS NULL OR [PreviousStatus] IN ('Pending', 'UnderReview', 'NeedsChanges', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationApprovals_Status",
                table: "PartnerApplicationApprovals",
                sql: "[Status] IN ('Pending', 'UnderReview', 'NeedsChanges', 'Approved', 'Rejected')");

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_MessageId_AttemptCount_Status",
                table: "Outgoing",
                columns: new[] { "MessageId", "AttemptCount", "Status" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Outgoing_Status",
                table: "Outgoing",
                sql: "[Status] IN ('Pending', 'Publishing', 'Failed', 'Published', 'LeaseExpired')");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptDateTime_CreatedDateTime",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptDateTime", "CreatedDateTime" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OutboxMessages_Status",
                table: "OutboxMessages",
                sql: "[Status] IN ('Pending', 'Publishing', 'Failed', 'Published')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_IncomingLogs_Status",
                table: "IncomingLogs",
                sql: "[Status] IN ('Received', 'Processing', 'Processed', 'Failed', 'Acknowledged', 'AcknowledgementFailed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Incoming_Status",
                table: "Incoming",
                sql: "[Status] IN ('Received', 'Processing', 'Processed', 'Failed')");

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
    }
}
