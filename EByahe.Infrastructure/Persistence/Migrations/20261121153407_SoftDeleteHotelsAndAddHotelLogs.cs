using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteHotelsAndAddHotelLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Hotels_PartnerId_Name_CodeDecodeBarangay",
                table: "Hotels");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "Hotels",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "Hotels",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Hotels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HotelLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    Action = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelLogs", x => x.Id);
                    table.CheckConstraint("CK_HotelLogs_Values", "[Action] IN ('Created','Updated','Deleted','Baseline') AND ISJSON([CurrentValue]) = 1 AND ([PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1)");
                    table.ForeignKey(
                        name: "FK_HotelLogs_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HotelLogs_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_DeletedBy",
                table: "Hotels",
                column: "DeletedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_PartnerId_Name_CodeDecodeBarangay",
                table: "Hotels",
                columns: new[] { "PartnerId", "Name", "CodeDecodeBarangay" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Hotels_Deletion",
                table: "Hotels",
                sql: "([IsDeleted] = 0 AND [DeletedBy] IS NULL AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [DeletedBy] IS NOT NULL AND [DeletedAtUtc] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_HotelLogs_CreatedBy",
                table: "HotelLogs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_HotelLogs_HotelId_DateTimeCreated",
                table: "HotelLogs",
                columns: new[] { "HotelId", "DateTimeCreated" });

            migrationBuilder.AddForeignKey(
                name: "FK_Hotels_RefUsers_DeletedBy",
                table: "Hotels",
                column: "DeletedBy",
                principalTable: "RefUsers",
                principalColumn: "ExternalUserId",
                onDelete: ReferentialAction.Restrict);

            // Existing rows get a baseline of their current state, not invented creation history.
            migrationBuilder.Sql("""
                INSERT INTO [HotelLogs] ([Id], [HotelId], [CreatedBy], [DateTimeCreated], [Action], [PreviousValue], [CurrentValue])
                SELECT NEWID(), h.[Id], N'System', SYSUTCDATETIME(), 'Baseline', NULL,
                  (SELECT h.[Id] AS [id], h.[PartnerId] AS [partnerId], h.[Name] AS [name], h.[Address] AS [address],
                    h.[CodeDecodeRegion] AS [codeDecodeRegion], h.[CodeDecodeProvince] AS [codeDecodeProvince],
                    h.[CodeDecodeMunicipality] AS [codeDecodeMunicipality], h.[CodeDecodeBarangay] AS [codeDecodeBarangay],
                    h.[ContactNumber] AS [contactNumber], h.[Email] AS [email], h.[Description] AS [description],
                    h.[IsActive] AS [isActive], h.[IsDeleted] AS [isDeleted], h.[DeletedBy] AS [deletedBy],
                    h.[DeletedAtUtc] AS [deletedAtUtc], h.[CreatedBy] AS [createdBy], h.[CreatedAtUtc] AS [createdAtUtc]
                   FOR JSON PATH, INCLUDE_NULL_VALUES, WITHOUT_ARRAY_WRAPPER)
                FROM [Hotels] h;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Hotels_RefUsers_DeletedBy",
                table: "Hotels");

            migrationBuilder.DropTable(
                name: "HotelLogs");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_DeletedBy",
                table: "Hotels");

            migrationBuilder.DropIndex(
                name: "IX_Hotels_PartnerId_Name_CodeDecodeBarangay",
                table: "Hotels");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Hotels_Deletion",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Hotels");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Hotels");

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_PartnerId_Name_CodeDecodeBarangay",
                table: "Hotels",
                columns: new[] { "PartnerId", "Name", "CodeDecodeBarangay" },
                unique: true);
        }
    }
}
