using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerBankProofAndWordUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationDocuments_ContentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationDocuments_DocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationDocuments_ContentType",
                table: "PartnerApplicationDocuments",
                sql: "[ContentType] IN ('application/pdf', 'image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationDocuments_DocumentType",
                table: "PartnerApplicationDocuments",
                sql: "[DocumentType] IN ('registration', 'bir', 'permit', 'accreditation', 'bankAccountProof')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationDocuments_ContentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PartnerApplicationDocuments_DocumentType",
                table: "PartnerApplicationDocuments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationDocuments_ContentType",
                table: "PartnerApplicationDocuments",
                sql: "[ContentType] IN ('application/pdf', 'image/jpeg', 'image/png')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PartnerApplicationDocuments_DocumentType",
                table: "PartnerApplicationDocuments",
                sql: "[DocumentType] IN ('registration', 'bir', 'permit', 'accreditation')");
        }
    }
}
