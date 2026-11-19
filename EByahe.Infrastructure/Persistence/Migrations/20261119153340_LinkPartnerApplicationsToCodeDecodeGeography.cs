using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkPartnerApplicationsToCodeDecodeGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBarangay",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeMunicipality",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeProvince",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeRegion",
                table: "PartnerApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "PartnerApplications",
                columns: new[] { "CodeDecodeBarangay", "CodeDecodeMunicipality" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "PartnerApplications",
                columns: new[] { "CodeDecodeMunicipality", "CodeDecodeProvince" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeProvince_CodeDecodeRegion",
                table: "PartnerApplications",
                columns: new[] { "CodeDecodeProvince", "CodeDecodeRegion" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CodeDecodeRegion",
                table: "PartnerApplications",
                column: "CodeDecodeRegion");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplications_Geography",
                table: "PartnerApplications",
                sql: "([CodeDecodeRegion] IS NULL AND [CodeDecodeProvince] IS NULL AND [CodeDecodeMunicipality] IS NULL AND [CodeDecodeBarangay] IS NULL)\nOR ([CodeDecodeRegion] IS NOT NULL AND [CodeDecodeProvince] IS NOT NULL AND [CodeDecodeMunicipality] IS NOT NULL AND [CodeDecodeBarangay] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeBarangays_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "PartnerApplications",
                columns: new[] { "CodeDecodeBarangay", "CodeDecodeMunicipality" },
                principalTable: "CodeDecodeBarangays",
                principalColumns: new[] { "Code", "CodeDecodeMunicipality" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeMunicipalities_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "PartnerApplications",
                columns: new[] { "CodeDecodeMunicipality", "CodeDecodeProvince" },
                principalTable: "CodeDecodeMunicipalities",
                principalColumns: new[] { "Code", "CodeDecodeProvince" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeProvinces_CodeDecodeProvince_CodeDecodeRegion",
                table: "PartnerApplications",
                columns: new[] { "CodeDecodeProvince", "CodeDecodeRegion" },
                principalTable: "CodeDecodeProvinces",
                principalColumns: new[] { "Code", "CodeDecodeRegion" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PartnerApplications_CodeDecodeRegions_CodeDecodeRegion",
                table: "PartnerApplications",
                column: "CodeDecodeRegion",
                principalTable: "CodeDecodeRegions",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeBarangays_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeMunicipalities_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeProvinces_CodeDecodeProvince_CodeDecodeRegion",
                table: "PartnerApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_PartnerApplications_CodeDecodeRegions_CodeDecodeRegion",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeProvince_CodeDecodeRegion",
                table: "PartnerApplications");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CodeDecodeRegion",
                table: "PartnerApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplications_Geography",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBarangay",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeMunicipality",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeProvince",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CodeDecodeRegion",
                table: "PartnerApplications");
        }
    }
}
