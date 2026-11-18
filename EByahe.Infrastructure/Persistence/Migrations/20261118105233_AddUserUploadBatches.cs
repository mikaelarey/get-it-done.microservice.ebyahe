using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUploadBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserUploadBatches",
                columns: table => new
                {
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedCount = table.Column<int>(type: "int", nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUploadBatches", x => x.UploadId);
                    table.CheckConstraint("CK_UserUploadBatches_Count", "[RequestedCount] BETWEEN 1 AND 500");
                    table.ForeignKey(
                        name: "FK_UserUploadBatches_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserUploadBatches_Uploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "Uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UploadMemberRequests",
                columns: table => new
                {
                    UploadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadMemberRequests", x => new { x.UploadId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_UploadMemberRequests_PartnerMemberRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PartnerMemberRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UploadMemberRequests_UserUploadBatches_UploadId",
                        column: x => x.UploadId,
                        principalTable: "UserUploadBatches",
                        principalColumn: "UploadId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UploadMemberRequests_RequestId",
                table: "UploadMemberRequests",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserUploadBatches_PartnerId",
                table: "UserUploadBatches",
                column: "PartnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadMemberRequests");

            migrationBuilder.DropTable(
                name: "UserUploadBatches");
        }
    }
}
