using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FormatLocationNamesProvinceFirst : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Geography codes are authoritative; leave unlinked legacy locations unchanged.
            migrationBuilder.Sql("""
                UPDATE location
                SET [Name] = province.[Decode] + N' - ' + municipality.[Decode]
                FROM [Locations] AS location
                INNER JOIN [CodeDecodeMunicipalities] AS municipality
                    ON municipality.[Code] = location.[CodeDecodeMunicipality]
                    AND municipality.[CodeDecodeProvince] = location.[CodeDecodeProvince]
                INNER JOIN [CodeDecodeProvinces] AS province
                    ON province.[Code] = location.[CodeDecodeProvince];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE location
                SET [Name] = municipality.[Decode] + N' (' + province.[Decode] + N')'
                FROM [Locations] AS location
                INNER JOIN [CodeDecodeMunicipalities] AS municipality
                    ON municipality.[Code] = location.[CodeDecodeMunicipality]
                    AND municipality.[CodeDecodeProvince] = location.[CodeDecodeProvince]
                INNER JOIN [CodeDecodeProvinces] AS province
                    ON province.[Code] = location.[CodeDecodeProvince];
                """);
        }
    }
}
