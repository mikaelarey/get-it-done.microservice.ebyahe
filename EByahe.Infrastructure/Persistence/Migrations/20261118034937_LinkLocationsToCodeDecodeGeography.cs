using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkLocationsToCodeDecodeGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeBarangay",
                table: "Locations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeMunicipality",
                table: "Locations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeProvince",
                table: "Locations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeRegion",
                table: "Locations",
                type: "int",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CodeDecodeProvinces_Code_CodeDecodeRegion",
                table: "CodeDecodeProvinces",
                columns: new[] { "Code", "CodeDecodeRegion" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CodeDecodeMunicipalities_Code_CodeDecodeProvince",
                table: "CodeDecodeMunicipalities",
                columns: new[] { "Code", "CodeDecodeProvince" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_CodeDecodeBarangays_Code_CodeDecodeMunicipality",
                table: "CodeDecodeBarangays",
                columns: new[] { "Code", "CodeDecodeMunicipality" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CodeDecodeBarangay",
                table: "Locations",
                column: "CodeDecodeBarangay",
                unique: true,
                filter: "[CodeDecodeBarangay] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "Locations",
                columns: new[] { "CodeDecodeBarangay", "CodeDecodeMunicipality" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "Locations",
                columns: new[] { "CodeDecodeMunicipality", "CodeDecodeProvince" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CodeDecodeProvince_CodeDecodeRegion",
                table: "Locations",
                columns: new[] { "CodeDecodeProvince", "CodeDecodeRegion" });

            migrationBuilder.CreateIndex(
                name: "IX_Locations_CodeDecodeRegion",
                table: "Locations",
                column: "CodeDecodeRegion");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Locations_Geography",
                table: "Locations",
                sql: "([CodeDecodeRegion] IS NULL AND [CodeDecodeProvince] IS NULL AND [CodeDecodeMunicipality] IS NULL AND [CodeDecodeBarangay] IS NULL)\nOR ([CountryCode] = 'PH' AND [CodeDecodeRegion] IS NOT NULL AND [CodeDecodeProvince] IS NOT NULL AND [CodeDecodeMunicipality] IS NOT NULL AND [CodeDecodeBarangay] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_CodeDecodeBarangays_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "Locations",
                columns: new[] { "CodeDecodeBarangay", "CodeDecodeMunicipality" },
                principalTable: "CodeDecodeBarangays",
                principalColumns: new[] { "Code", "CodeDecodeMunicipality" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_CodeDecodeMunicipalities_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "Locations",
                columns: new[] { "CodeDecodeMunicipality", "CodeDecodeProvince" },
                principalTable: "CodeDecodeMunicipalities",
                principalColumns: new[] { "Code", "CodeDecodeProvince" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_CodeDecodeProvinces_CodeDecodeProvince_CodeDecodeRegion",
                table: "Locations",
                columns: new[] { "CodeDecodeProvince", "CodeDecodeRegion" },
                principalTable: "CodeDecodeProvinces",
                principalColumns: new[] { "Code", "CodeDecodeRegion" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Locations_CodeDecodeRegions_CodeDecodeRegion",
                table: "Locations",
                column: "CodeDecodeRegion",
                principalTable: "CodeDecodeRegions",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Locations_CodeDecodeBarangays_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_CodeDecodeMunicipalities_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_CodeDecodeProvinces_CodeDecodeProvince_CodeDecodeRegion",
                table: "Locations");

            migrationBuilder.DropForeignKey(
                name: "FK_Locations_CodeDecodeRegions_CodeDecodeRegion",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_CodeDecodeBarangay",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_CodeDecodeProvince_CodeDecodeRegion",
                table: "Locations");

            migrationBuilder.DropIndex(
                name: "IX_Locations_CodeDecodeRegion",
                table: "Locations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Locations_Geography",
                table: "Locations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CodeDecodeProvinces_Code_CodeDecodeRegion",
                table: "CodeDecodeProvinces");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CodeDecodeMunicipalities_Code_CodeDecodeProvince",
                table: "CodeDecodeMunicipalities");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_CodeDecodeBarangays_Code_CodeDecodeMunicipality",
                table: "CodeDecodeBarangays");

            migrationBuilder.DropColumn(
                name: "CodeDecodeBarangay",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CodeDecodeMunicipality",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CodeDecodeProvince",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "CodeDecodeRegion",
                table: "Locations");
        }
    }
}
