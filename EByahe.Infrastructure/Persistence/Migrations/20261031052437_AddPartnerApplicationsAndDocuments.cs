using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerApplicationsAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingId = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false, defaultValue: "Submitted"),
                    BusinessCategory = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TradeName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    Tin = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RepresentativeName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RepresentativeEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    RepresentativeMobile = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AccountHolder = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SettlementFrequency = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApplications", x => x.Id);
                    table.CheckConstraint("CK_PartnerApplications_BusinessCategory", "[BusinessCategory] IN ('hotel', 'bus-operator')");
                    table.CheckConstraint("CK_PartnerApplications_EntityType", "[EntityType] IN ('sole-proprietorship', 'partnership', 'corporation')");
                    table.CheckConstraint("CK_PartnerApplications_SettlementFrequency", "[SettlementFrequency] IN ('daily', 'weekly')");
                    table.CheckConstraint("CK_PartnerApplications_Status", "[Status] IN ('Submitted', 'UnderReview', 'Approved', 'Rejected')");
                });

            migrationBuilder.CreateTable(
                name: "PartnerApplicationDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApplicationDocuments", x => x.Id);
                    table.CheckConstraint("CK_PartnerApplicationDocuments_ContentType", "[ContentType] IN ('application/pdf', 'image/jpeg', 'image/png')");
                    table.CheckConstraint("CK_PartnerApplicationDocuments_DocumentType", "[DocumentType] IN ('registration', 'bir', 'permit', 'accreditation')");
                    table.CheckConstraint("CK_PartnerApplicationDocuments_SizeBytes", "[SizeBytes] > 0 AND [SizeBytes] <= 10485760 AND DATALENGTH([Content]) = [SizeBytes]");
                    table.ForeignKey(
                        name: "FK_PartnerApplicationDocuments_PartnerApplications_PartnerApplicationId",
                        column: x => x.PartnerApplicationId,
                        principalTable: "PartnerApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationDocuments_PartnerApplicationId_DocumentType",
                table: "PartnerApplicationDocuments",
                columns: new[] { "PartnerApplicationId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_Status_SubmittedAtUtc",
                table: "PartnerApplications",
                columns: new[] { "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_TrackingId",
                table: "PartnerApplications",
                column: "TrackingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerApplicationDocuments");

            migrationBuilder.DropTable(
                name: "PartnerApplications");
        }
    }
}
