using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EByahe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicCodeDecode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Incoming_ConsumerGroup_Topic_Partition_Offset",
                table: "Incoming");

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeTopic",
                table: "Outgoing",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeTopic",
                table: "OutboxMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CodeDecodeTopic",
                table: "Incoming",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CodeDecodeTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Decode = table.Column<string>(type: "varchar(249)", unicode: false, maxLength: 249, nullable: false, collation: "Latin1_General_100_BIN2"),
                    CreatedBy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: "System", collation: "Latin1_General_100_BIN2"),
                    DateTimeCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeDecodeTopics", x => x.Id);
                    table.UniqueConstraint("AK_CodeDecodeTopics_Code", x => x.Code);
                    table.ForeignKey(
                        name: "FK_CodeDecodeTopics_RefUsers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "RefUsers",
                        principalColumn: "ExternalUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CodeDecodeTopics",
                columns: new[] { "Id", "Code", "CreatedBy", "DateTimeCreated", "Decode" },
                values: new object[,]
                {
                    { new Guid("479cf819-8d46-48a6-9479-000000000001"), 1, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "get-it-done.notifications.v1" },
                    { new Guid("479cf819-8d46-48a6-9479-000000000002"), 2, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "get-it-done.ebyahe.partners.v1" },
                    { new Guid("479cf819-8d46-48a6-9479-000000000003"), 3, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "get-it-done.ebyahe.partner-members.v1" },
                    { new Guid("479cf819-8d46-48a6-9479-000000000004"), 4, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "get-it-done.account.users.v1" },
                    { new Guid("479cf819-8d46-48a6-9479-000000000005"), 5, "System", new DateTimeOffset(new DateTime(2026, 11, 19, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "get-it-done.payment.completed.v1" }
                });

            migrationBuilder.Sql("""
                INSERT INTO [CodeDecodeTopics] ([Id],[Decode],[CreatedBy])
                  SELECT NEWID(), names.[Topic], 'System' FROM
                    (SELECT [Topic] COLLATE Latin1_General_100_BIN2 AS [Topic] FROM [OutboxMessages]
                     UNION SELECT [Topic] COLLATE Latin1_General_100_BIN2 FROM [Outgoing]
                     UNION SELECT [Topic] COLLATE Latin1_General_100_BIN2 FROM [Incoming]) names
                  WHERE NOT EXISTS (SELECT 1 FROM [CodeDecodeTopics] setup WHERE setup.[Decode]=names.[Topic]);
                UPDATE source SET [CodeDecodeTopic]=setup.[Code] FROM [OutboxMessages] source JOIN [CodeDecodeTopics] setup ON setup.[Decode]=source.[Topic] COLLATE Latin1_General_100_BIN2;
                UPDATE source SET [CodeDecodeTopic]=setup.[Code] FROM [Outgoing] source JOIN [CodeDecodeTopics] setup ON setup.[Decode]=source.[Topic] COLLATE Latin1_General_100_BIN2;
                UPDATE source SET [CodeDecodeTopic]=setup.[Code] FROM [Incoming] source JOIN [CodeDecodeTopics] setup ON setup.[Decode]=source.[Topic] COLLATE Latin1_General_100_BIN2;
                """);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeTopic", table: "OutboxMessages", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeTopic", table: "Outgoing", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "CodeDecodeTopic", table: "Incoming", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "Outgoing");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "Incoming");

            migrationBuilder.CreateIndex(
                name: "IX_Outgoing_CodeDecodeTopic",
                table: "Outgoing",
                column: "CodeDecodeTopic");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CodeDecodeTopic",
                table: "OutboxMessages",
                column: "CodeDecodeTopic");

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_CodeDecodeTopic",
                table: "Incoming",
                column: "CodeDecodeTopic");

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_ConsumerGroup_CodeDecodeTopic_Partition_Offset",
                table: "Incoming",
                columns: new[] { "ConsumerGroup", "CodeDecodeTopic", "Partition", "Offset" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeTopics_CreatedBy",
                table: "CodeDecodeTopics",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CodeDecodeTopics_Decode",
                table: "CodeDecodeTopics",
                column: "Decode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incoming_CodeDecodeTopics_CodeDecodeTopic",
                table: "Incoming",
                column: "CodeDecodeTopic",
                principalTable: "CodeDecodeTopics",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OutboxMessages_CodeDecodeTopics_CodeDecodeTopic",
                table: "OutboxMessages",
                column: "CodeDecodeTopic",
                principalTable: "CodeDecodeTopics",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Outgoing_CodeDecodeTopics_CodeDecodeTopic",
                table: "Outgoing",
                column: "CodeDecodeTopic",
                principalTable: "CodeDecodeTopics",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "Outgoing",
                type: "varchar(249)",
                unicode: false,
                maxLength: 249,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "OutboxMessages",
                type: "varchar(249)",
                unicode: false,
                maxLength: 249,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "Incoming",
                type: "varchar(249)",
                unicode: false,
                maxLength: 249,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE source SET [Topic]=setup.[Decode] FROM [OutboxMessages] source JOIN [CodeDecodeTopics] setup ON setup.[Code]=source.[CodeDecodeTopic];
                UPDATE source SET [Topic]=setup.[Decode] FROM [Outgoing] source JOIN [CodeDecodeTopics] setup ON setup.[Code]=source.[CodeDecodeTopic];
                UPDATE source SET [Topic]=setup.[Decode] FROM [Incoming] source JOIN [CodeDecodeTopics] setup ON setup.[Code]=source.[CodeDecodeTopic];
                """);
            migrationBuilder.AlterColumn<string>(name: "Topic", table: "OutboxMessages", type: "varchar(249)", unicode: false, maxLength: 249, nullable: false, oldClrType: typeof(string), oldType: "varchar(249)", oldUnicode: false, oldMaxLength: 249, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "Topic", table: "Outgoing", type: "varchar(249)", unicode: false, maxLength: 249, nullable: false, oldClrType: typeof(string), oldType: "varchar(249)", oldUnicode: false, oldMaxLength: 249, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "Topic", table: "Incoming", type: "varchar(249)", unicode: false, maxLength: 249, nullable: false, oldClrType: typeof(string), oldType: "varchar(249)", oldUnicode: false, oldMaxLength: 249, oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Incoming_CodeDecodeTopics_CodeDecodeTopic",
                table: "Incoming");

            migrationBuilder.DropForeignKey(
                name: "FK_OutboxMessages_CodeDecodeTopics_CodeDecodeTopic",
                table: "OutboxMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Outgoing_CodeDecodeTopics_CodeDecodeTopic",
                table: "Outgoing");

            migrationBuilder.DropTable(
                name: "CodeDecodeTopics");

            migrationBuilder.DropIndex(
                name: "IX_Outgoing_CodeDecodeTopic",
                table: "Outgoing");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_CodeDecodeTopic",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Incoming_CodeDecodeTopic",
                table: "Incoming");

            migrationBuilder.DropIndex(
                name: "IX_Incoming_ConsumerGroup_CodeDecodeTopic_Partition_Offset",
                table: "Incoming");

            migrationBuilder.DropColumn(
                name: "CodeDecodeTopic",
                table: "Outgoing");

            migrationBuilder.DropColumn(
                name: "CodeDecodeTopic",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "CodeDecodeTopic",
                table: "Incoming");

            migrationBuilder.CreateIndex(
                name: "IX_Incoming_ConsumerGroup_Topic_Partition_Offset",
                table: "Incoming",
                columns: new[] { "ConsumerGroup", "Topic", "Partition", "Offset" },
                unique: true);
        }
    }
}
