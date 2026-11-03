using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerApplicationCreatorHistoryAndApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "PartnerApplications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.CreateTable(
                name: "PartnerApplicationApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousStatus = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, collation: "Latin1_General_100_BIN2"),
                    Remarks = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApplicationApprovals", x => x.Id);
                    table.CheckConstraint("CK_PartnerApplicationApprovals_PreviousStatus", "[PreviousStatus] IS NULL OR [PreviousStatus] IN ('Pending', 'UnderReview', 'NeedsChanges', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_PartnerApplicationApprovals_Status", "[Status] IN ('Pending', 'UnderReview', 'NeedsChanges', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_PartnerApplicationApprovals_PartnerApplications_PartnerApplicationId",
                        column: x => x.PartnerApplicationId,
                        principalTable: "PartnerApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartnerApplicationHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerApplicationHistories", x => x.Id);
                    table.CheckConstraint("CK_PartnerApplicationHistories_CurrentValue", "ISJSON([CurrentValue]) = 1");
                    table.CheckConstraint("CK_PartnerApplicationHistories_PreviousValue", "[PreviousValue] IS NULL OR ISJSON([PreviousValue]) = 1");
                    table.CheckConstraint("CK_PartnerApplicationHistories_Status", "[Status] IN ('Submitted', 'UnderReview', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_PartnerApplicationHistories_PartnerApplications_PartnerApplicationId",
                        column: x => x.PartnerApplicationId,
                        principalTable: "PartnerApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplications_CreatedBy_SubmittedAtUtc",
                table: "PartnerApplications",
                columns: new[] { "CreatedBy", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationApprovals_PartnerApplicationId_DateTimeCreated",
                table: "PartnerApplicationApprovals",
                columns: new[] { "PartnerApplicationId", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationApprovals_Status_DateTimeCreated",
                table: "PartnerApplicationApprovals",
                columns: new[] { "Status", "DateTimeCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApplicationHistories_PartnerApplicationId_DateTimeCreated",
                table: "PartnerApplicationHistories",
                columns: new[] { "PartnerApplicationId", "DateTimeCreated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartnerApplicationApprovals");

            migrationBuilder.DropTable(
                name: "PartnerApplicationHistories");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApplications_CreatedBy_SubmittedAtUtc",
                table: "PartnerApplications");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "PartnerApplications");
        }
    }
}
