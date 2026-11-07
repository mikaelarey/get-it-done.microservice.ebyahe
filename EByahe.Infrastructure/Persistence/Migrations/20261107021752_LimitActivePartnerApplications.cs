using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LimitActivePartnerApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_PartnerApplications_ActiveCreator",
                table: "PartnerApplications",
                column: "CreatedBy",
                unique: true,
                filter: "[CreatedBy] IS NOT NULL AND [Status] IN ('Submitted', 'UnderReview')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PartnerApplications_ActiveCreator",
                table: "PartnerApplications");
        }
    }
}
