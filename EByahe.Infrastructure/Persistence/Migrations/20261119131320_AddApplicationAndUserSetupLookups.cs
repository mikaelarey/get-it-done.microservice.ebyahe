using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationAndUserSetupLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RefUsers_Role",
                table: "RefUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplications_EntityType",
                table: "PartnerApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplications_SettlementFrequency",
                table: "PartnerApplications");

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeRole",
                table: "RefUsers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBank",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeEntityType",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeSettlementFrequency",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeDecodeBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeBanks", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeBanks_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeBanks_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeEntityTypes",
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
                    table.PrimaryKey("PK_CodeDecodeEntityTypes", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeEntityTypes_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeEntityTypes_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeRoles", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeRoles_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeRoles_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeSettlementFrequencies",
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
                    table.PrimaryKey("PK_CodeDecodeSettlementFrequencies", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeSettlementFrequencies_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeSettlementFrequencies_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeBanks",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "BDO" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "BPI" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000003"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Metrobank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000004"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Land Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000005"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "PNB" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000006"), 6, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "RCBC" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000007"), 7, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Security Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000008"), 8, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnionBank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000009"), 9, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "BDO Unibank, Inc." },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000010"), 10, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bank of the Philippine Islands (BPI)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000011"), 11, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Metropolitan Bank and Trust Company (Metrobank)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000012"), 12, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Land Bank of the Philippines (LANDBANK)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000013"), 13, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Philippine National Bank (PNB)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000014"), 14, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Security Bank Corporation" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000015"), 15, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "China Banking Corporation (China Bank)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000016"), 16, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Union Bank of the Philippines (UnionBank)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000017"), 17, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Rizal Commercial Banking Corporation (RCBC)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000018"), 18, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Development Bank of the Philippines (DBP)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000019"), 19, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Asia United Bank Corporation (AUB)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000020"), 20, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bank of Commerce" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000021"), 21, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Philippine Veterans Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000022"), 22, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Al-Amanah Islamic Investment Bank of the Philippines" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000023"), 23, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "BPI Family Savings Bank / BPI Direct BanKo" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000024"), 24, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "China Bank Savings, Inc." },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000025"), 25, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Philippine Savings Bank (PSBank)" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000026"), 26, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "City Savings Bank Inc." },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000027"), 27, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Philippine Business Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000028"), 28, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Equicom Savings Bank, Inc." },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000029"), 29, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ISLA Bank (A Thrift Bank), Inc." },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000030"), 30, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Maya Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000031"), 31, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "GoTyme Bank Corporation" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000032"), 32, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Tonik Digital Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000033"), 33, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UnionDigital Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000034"), 34, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Maribank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000035"), 35, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UNO Digital Bank" },
                    { new Guid("b4c11a64-acb7-4d86-8b09-000000000036"), 36, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Overseas Filipino Bank (OFBank)" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeEntityTypes",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("b4c11a61-acb7-4d86-8b09-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sole Proprietorship" },
                    { new Guid("b4c11a61-acb7-4d86-8b09-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Partnership" },
                    { new Guid("b4c11a61-acb7-4d86-8b09-000000000003"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Corporation" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeRoles",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("b4c11a63-acb7-4d86-8b09-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Normal" },
                    { new Guid("b4c11a63-acb7-4d86-8b09-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Administrator" }
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeSettlementFrequencies",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("b4c11a62-acb7-4d86-8b09-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Daily" },
                    { new Guid("b4c11a62-acb7-4d86-8b09-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Weekly" }
                });

            migrationBuilder.UpdateData(
                table: "RefUsers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-4000-8000-000000000001"),
                column: "CodeDecodeRole",
                value: 1);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplications] WHERE [EntityType] NOT IN ('sole-proprietorship','partnership','corporation') OR [SettlementFrequency] NOT IN ('daily','weekly'))
                   OR EXISTS (SELECT 1 FROM [RefUsers] WHERE [Role] COLLATE Latin1_General_100_BIN2 NOT IN ('Normal','Administrator'))
                  THROW 51000, 'Unmapped entity type, settlement frequency, or role; register the value before migrating.', 1;
                INSERT INTO [CodeDecodeBanks] ([Id],[Decode],[CreatedBy])
                  SELECT NEWID(), names.[BankName], 'System' FROM
                    (SELECT DISTINCT [BankName] COLLATE Latin1_General_100_BIN2 AS [BankName] FROM [PartnerApplications] WHERE [BankName]<>'') names
                  WHERE NOT EXISTS (SELECT 1 FROM [CodeDecodeBanks] bank WHERE bank.[Decode]=names.[BankName]);
                UPDATE application SET [CodeDecodeBank]=bank.[Code]
                  FROM [PartnerApplications] application LEFT JOIN [CodeDecodeBanks] bank ON bank.[Decode]=application.[BankName] COLLATE Latin1_General_100_BIN2;
                UPDATE [PartnerApplications] SET [CodeDecodeEntityType]=CASE [EntityType] WHEN 'sole-proprietorship' THEN 1 WHEN 'partnership' THEN 2 WHEN 'corporation' THEN 3 END,
                  [CodeDecodeSettlementFrequency]=CASE [SettlementFrequency] WHEN 'daily' THEN 1 WHEN 'weekly' THEN 2 END;
                UPDATE [RefUsers] SET [CodeDecodeRole]=CASE [Role] WHEN 'Normal' THEN 1 WHEN 'Administrator' THEN 2 END;
                """);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeEntityType", table: "PartnerApplications", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeSettlementFrequency", table: "PartnerApplications", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Role",
                table: "RefUsers");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "SettlementFrequency",
                table: "PartnerApplications");

            migrationBuilder.CreateIndex(
                name: "IX_RefUsers_CodeDecodeRole",
                table: "RefUsers",
                column: "CodeDecodeRole");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeBank",
                table: "PartnerApplications",
                column: "CodeDecodeBank");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeEntityType",
                table: "PartnerApplications",
                column: "CodeDecodeEntityType");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeSettlementFrequency",
                table: "PartnerApplications",
                column: "CodeDecodeSettlementFrequency");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBanks_CreatedBy",
                table: "CodeDecodeBanks",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBanks_Decode",
                table: "CodeDecodeBanks",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeEntityTypes_CreatedBy",
                table: "CodeDecodeEntityTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeEntityTypes_Decode",
                table: "CodeDecodeEntityTypes",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeRoles_CreatedBy",
                table: "CodeDecodeRoles",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeRoles_Decode",
                table: "CodeDecodeRoles",
                column: "Decode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeSettlementFrequencies_CreatedBy",
                table: "CodeDecodeSettlementFrequencies",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeSettlementFrequencies_Decode",
                table: "CodeDecodeSettlementFrequencies",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeBanks_CodeDecodeBank",
                table: "PartnerApplications",
                column: "CodeDecodeBank",
                principalTable: "CodeDecodeBanks",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeEntityTypes_CodeDecodeEntityType",
                table: "PartnerApplications",
                column: "CodeDecodeEntityType",
                principalTable: "CodeDecodeEntityTypes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeSettlementFrequencies_CodeDecodeSettlementFrequency",
                table: "PartnerApplications",
                column: "CodeDecodeSettlementFrequency",
                principalTable: "CodeDecodeSettlementFrequencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefUsers_CodeDecodeRoles_CodeDecodeRole",
                table: "RefUsers",
                column: "CodeDecodeRole",
                principalTable: "CodeDecodeRoles",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "RefUsers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                defaultValue: "Normal",
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "PartnerApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                defaultValue: null);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "PartnerApplications",
                type: "varchar(32)",
                unicode: false,
                maxLength: 32,
                nullable: true,
                defaultValue: null);

            migrationBuilder.AddColumn<string>(
                name: "SettlementFrequency",
                table: "PartnerApplications",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: true,
                defaultValue: null);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [PartnerApplications] WHERE [CodeDecodeEntityType] NOT IN (1,2,3) OR [CodeDecodeSettlementFrequency] NOT IN (1,2))
                   OR EXISTS (SELECT 1 FROM [RefUsers] WHERE [CodeDecodeRole] NOT IN (1,2))
                  THROW 51000, 'A setup value cannot be represented by the previous schema.', 1;
                UPDATE application SET [BankName]=ISNULL(bank.[Decode],'')
                  FROM [PartnerApplications] application LEFT JOIN [CodeDecodeBanks] bank ON bank.[Code]=application.[CodeDecodeBank];
                UPDATE [PartnerApplications] SET [EntityType]=CASE [CodeDecodeEntityType] WHEN 1 THEN 'sole-proprietorship' WHEN 2 THEN 'partnership' WHEN 3 THEN 'corporation' END,
                  [SettlementFrequency]=CASE [CodeDecodeSettlementFrequency] WHEN 1 THEN 'daily' WHEN 2 THEN 'weekly' END;
                UPDATE [RefUsers] SET [Role]=CASE [CodeDecodeRole] WHEN 1 THEN 'Normal' WHEN 2 THEN 'Administrator' END;
                """);
            migrationBuilder.AlterColumn<string>(name: "Role", table: "RefUsers", type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "Normal", collation: "Latin1_General_100_BIN2", oldClrType: typeof(string), oldType: "nvarchar(32)", oldMaxLength: 32, oldNullable: true, oldDefaultValue: "Normal", oldCollation: "Latin1_General_100_BIN2");
            migrationBuilder.AlterColumn<string>(name: "BankName", table: "PartnerApplications", type: "nvarchar(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "nvarchar(100)", oldMaxLength: 100, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "EntityType", table: "PartnerApplications", type: "varchar(32)", unicode: false, maxLength: 32, nullable: false, oldClrType: typeof(string), oldType: "varchar(32)", oldUnicode: false, oldMaxLength: 32, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "SettlementFrequency", table: "PartnerApplications", type: "varchar(16)", unicode: false, maxLength: 16, nullable: false, oldClrType: typeof(string), oldType: "varchar(16)", oldUnicode: false, oldMaxLength: 16, oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeBanks_CodeDecodeBank",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeEntityTypes_CodeDecodeEntityType",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeSettlementFrequencies_CodeDecodeSettlementFrequency",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_RefUsers_CodeDecodeRoles_CodeDecodeRole",
                table: "RefUsers");

            migrationBuilder.DropTable(
                name: "CodeDecodeBanks");

            migrationBuilder.DropTable(
                name: "CodeDecodeEntityTypes");

            migrationBuilder.DropTable(
                name: "CodeDecodeRoles");

            migrationBuilder.DropTable(
                name: "CodeDecodeSettlementFrequencies");

            migrationBuilder.DropIndex(
                name: "IX_RefUsers_CodeDecodeRole",
                table: "RefUsers");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeBank",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeEntityType",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeSettlementFrequency",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeRole",
                table: "RefUsers");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBank",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeEntityType",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeSettlementFrequency",
                table: "PartnerApplications");

            migrationBuilder.UpdateData(
                table: "RefUsers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-4000-8000-000000000001"),
                column: "Role",
                value: "Normal");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RefUsers_Role",
                table: "RefUsers",
                sql: "[Role] IN ('Administrator', 'Normal')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplications_EntityType",
                table: "PartnerApplications",
                sql: "[EntityType] IN ('sole-proprietorship', 'partnership', 'corporation')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplications_SettlementFrequency",
                table: "PartnerApplications",
                sql: "[SettlementFrequency] IN ('daily', 'weekly')");
        }
    }
}
