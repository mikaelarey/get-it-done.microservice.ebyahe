using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerHotels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hotels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CodeDecodeRegion = table.Column<int>(type: "int", nullable: false),
                    CodeDecodeProvince = table.Column<int>(type: "int", nullable: false),
                    CodeDecodeMunicipality = table.Column<int>(type: "int", nullable: false),
                    CodeDecodeBarangay = table.Column<int>(type: "int", nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hotels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hotels_CodeDecodeBarangays_CodeDecodeBarangay_CodeDecodeMunicipality",
                        columns: x => new { x.CodeDecodeBarangay, x.CodeDecodeMunicipality },
                        principalTable: "CodeDecodeBarangays",
                        principalColumns: new[] { "Code", "CodeDecodeMunicipality" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hotels_CodeDecodeMunicipalities_CodeDecodeMunicipality_CodeDecodeProvince",
                        columns: x => new { x.CodeDecodeMunicipality, x.CodeDecodeProvince },
                        principalTable: "CodeDecodeMunicipalities",
                        principalColumns: new[] { "Code", "CodeDecodeProvince" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hotels_CodeDecodeProvinces_CodeDecodeProvince_CodeDecodeRegion",
                        columns: x => new { x.CodeDecodeProvince, x.CodeDecodeRegion },
                        principalTable: "CodeDecodeProvinces",
                        principalColumns: new[] { "Code", "CodeDecodeRegion" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hotels_CodeDecodeRegions_CodeDecodeRegion",
                        column: x => x.CodeDecodeRegion,
                        principalTable: "CodeDecodeRegions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hotels_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Hotels_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_CodeDecodeBarangay_CodeDecodeMunicipality",
                table: "Hotels",
                columns: new[] { "CodeDecodeBarangay", "CodeDecodeMunicipality" });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_CodeDecodeMunicipality_CodeDecodeProvince",
                table: "Hotels",
                columns: new[] { "CodeDecodeMunicipality", "CodeDecodeProvince" });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_CodeDecodeProvince_CodeDecodeRegion",
                table: "Hotels",
                columns: new[] { "CodeDecodeProvince", "CodeDecodeRegion" });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_CodeDecodeRegion",
                table: "Hotels",
                column: "CodeDecodeRegion");

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_CreatedBy",
                table: "Hotels",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_PartnerId_Name_CodeDecodeBarangay",
                table: "Hotels",
                columns: new[] { "PartnerId", "Name", "CodeDecodeBarangay" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Hotels");
        }
    }
}
