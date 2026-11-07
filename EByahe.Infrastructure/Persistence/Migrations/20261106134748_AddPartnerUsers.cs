using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [Partners] p WHERE p.[OwnerUserId] IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [RefUsers] u WHERE u.[ExternalUserId] = p.[OwnerUserId]))
                  THROW 51030, 'A partner owner has no local RefUser. Backfill Account references before applying AddPartnerUsers.', 1;
                """);
            migrationBuilder.CreateTable(
                name: "PartnerUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RefUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerUsers_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartnerUsers_RefUsers_RefUserId",
                        column: x => x.RefUserId,
                        principalTable: "RefUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerUsers_PartnerId_RefUserId",
                table: "PartnerUsers",
                columns: new[] { "PartnerId", "RefUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerUsers_RefUserId_IsActive",
                table: "PartnerUsers",
                columns: new[] { "RefUserId", "IsActive" });

            // Keep the original external owner subject and add its local GUID membership.
            migrationBuilder.Sql("""
                INSERT INTO [PartnerUsers] ([Id], [PartnerId], [RefUserId], [IsActive], [CreatedAtUtc])
                SELECT NEWID(), p.[Id], u.[Id], 1, SYSUTCDATETIME()
                FROM [Partners] p JOIN [RefUsers] u ON u.[ExternalUserId] = p.[OwnerUserId];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerUsers");
        }
    }
}
