using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Diwan_Correspondence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiwanReplyId",
                table: "StudyStartOrders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudyDiwanCases",
                columns: table => new
                {
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    DispatchNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DispatchDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyDiwanCases", x => x.ResponseId);
                    table.CheckConstraint("CK_StudyDiwanCases_Stage", "[Stage] BETWEEN 2 AND 7");
                    table.ForeignKey(
                        name: "FK_StudyDiwanCases_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyDiwanReplies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyDiwanReplies", x => x.Id);
                    table.UniqueConstraint("AK_StudyDiwanReplies_ResponseId_Id", x => new { x.ResponseId, x.Id });
                    table.CheckConstraint("CK_StudyDiwanReplies_Decision", "[Decision] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_StudyDiwanReplies_StudyDiwanCases_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyDiwanCases",
                        principalColumn: "ResponseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyStartOrders_ResponseId_DiwanReplyId",
                table: "StudyStartOrders",
                columns: new[] { "ResponseId", "DiwanReplyId" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyDiwanCases_Stage",
                table: "StudyDiwanCases",
                column: "Stage");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyStartOrders_StudyDiwanReplies_ResponseId_DiwanReplyId",
                table: "StudyStartOrders",
                columns: new[] { "ResponseId", "DiwanReplyId" },
                principalTable: "StudyDiwanReplies",
                principalColumns: new[] { "ResponseId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyStartOrders_StudyDiwanReplies_ResponseId_DiwanReplyId",
                table: "StudyStartOrders");

            migrationBuilder.DropTable(
                name: "StudyDiwanReplies");

            migrationBuilder.DropTable(
                name: "StudyDiwanCases");

            migrationBuilder.DropIndex(
                name: "IX_StudyStartOrders_ResponseId_DiwanReplyId",
                table: "StudyStartOrders");

            migrationBuilder.DropColumn(
                name: "DiwanReplyId",
                table: "StudyStartOrders");
        }
    }
}
