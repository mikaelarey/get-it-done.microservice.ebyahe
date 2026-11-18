using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeDecodeGeography : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodeDecodeRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeRegions", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeRegions_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeRegions_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeProvinces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CodeDecodeRegion = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeProvinces", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeProvinces_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeProvinces_CodeDecodeRegions_CodeDecodeRegion",
                        column: x => x.CodeDecodeRegion,
                        principalTable: "CodeDecodeRegions",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CodeDecodeProvinces_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeMunicipalities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CodeDecodeProvince = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeMunicipalities", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeMunicipalities_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeMunicipalities_CodeDecodeProvinces_CodeDecodeProvince",
                        column: x => x.CodeDecodeProvince,
                        principalTable: "CodeDecodeProvinces",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CodeDecodeMunicipalities_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CodeDecodeBarangays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CodeDecodeMunicipality = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeBarangays", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeBarangays_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeBarangays_CodeDecodeMunicipalities_CodeDecodeMunicipality",
                        column: x => x.CodeDecodeMunicipality,
                        principalTable: "CodeDecodeMunicipalities",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CodeDecodeBarangays_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBarangays_CodeDecodeMunicipality_Decode",
                table: "CodeDecodeBarangays",
                columns: new[] { "CodeDecodeMunicipality", "Decode" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeBarangays_CreatedBy",
                table: "CodeDecodeBarangays",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeMunicipalities_CodeDecodeProvince_Decode",
                table: "CodeDecodeMunicipalities",
                columns: new[] { "CodeDecodeProvince", "Decode" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeMunicipalities_CreatedBy",
                table: "CodeDecodeMunicipalities",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeProvinces_CodeDecodeRegion_Decode",
                table: "CodeDecodeProvinces",
                columns: new[] { "CodeDecodeRegion", "Decode" });

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeProvinces_CreatedBy",
                table: "CodeDecodeProvinces",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeRegions_CreatedBy",
                table: "CodeDecodeRegions",
                column: "CreatedBy");

            SeedBatches(migrationBuilder, "CodeDecodeRegions");
            SeedBatches(migrationBuilder, "CodeDecodeProvinces");
            SeedBatches(migrationBuilder, "CodeDecodeMunicipalities");
            SeedBatches(migrationBuilder, "CodeDecodeBarangays");
        }

        private static void SeedBatches(MigrationBuilder migrationBuilder, string table)
        {
            // These versioned resources contain SQL Server inserts of at most 500 rows.
            // Execute each GO section as a separate command within EF's migration transaction.
            using var resource = typeof(AddCodeDecodeGeography).Assembly.GetManifestResourceStream(
                $"EByahe.Infrastructure.Persistence.Migrations.Scripts.Geography.{table}.sql")
                ?? throw new InvalidOperationException($"Missing geography migration data for {table}.");
            using var reader = new System.IO.StreamReader(resource, System.Text.Encoding.UTF8);
            var sql = reader.ReadToEnd().Replace("\r\n", "\n");
            foreach (var batch in sql.Split("\nGO\n", StringSplitOptions.RemoveEmptyEntries))
                migrationBuilder.Sql(batch);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodeDecodeBarangays");

            migrationBuilder.DropTable(
                name: "CodeDecodeMunicipalities");

            migrationBuilder.DropTable(
                name: "CodeDecodeProvinces");

            migrationBuilder.DropTable(
                name: "CodeDecodeRegions");
        }
    }
}
