using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task_Board_Enhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "AssignedTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClaimedByUserId",
                table: "AssignedTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueSoonNotifiedAt",
                table: "AssignedTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNotifiedAt",
                table: "AssignedTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssignedTaskAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignedTaskId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTaskAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignedTaskAttachments_AssignedTasks_AssignedTaskId",
                        column: x => x.AssignedTaskId,
                        principalTable: "AssignedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignedTaskAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignedTaskChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignedTaskId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTaskChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignedTaskChecklistItems_AssignedTasks_AssignedTaskId",
                        column: x => x.AssignedTaskId,
                        principalTable: "AssignedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssignedTaskAttachmentContents",
                columns: table => new
                {
                    AttachmentId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTaskAttachmentContents", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_AssignedTaskAttachmentContents_AssignedTaskAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "AssignedTaskAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTasks_ClaimedByUserId",
                table: "AssignedTasks",
                column: "ClaimedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTasks_Status_DueDate",
                table: "AssignedTasks",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskAttachments_AssignedTaskId",
                table: "AssignedTaskAttachments",
                column: "AssignedTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskAttachments_UploadedByUserId",
                table: "AssignedTaskAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskChecklistItems_AssignedTaskId_SortOrder",
                table: "AssignedTaskChecklistItems",
                columns: new[] { "AssignedTaskId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_AssignedTasks_Users_ClaimedByUserId",
                table: "AssignedTasks",
                column: "ClaimedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // المهام المتأخرة أصلاً قبل التحديث: تُعلَّم «أُبلغ عنها» كي لا تنفجر إشعارات التأخر دفعة واحدة عند أول فحص —
            // التذكيرات تبدأ من المواعيد القادمة وما يتأخر بعد الآن
            migrationBuilder.Sql(@"
UPDATE AssignedTasks
SET OverdueNotifiedAt = SYSUTCDATETIME(), DueSoonNotifiedAt = SYSUTCDATETIME()
WHERE Status <> 3 AND DueDate IS NOT NULL AND CAST(DueDate AS date) < CAST(GETDATE() AS date);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssignedTasks_Users_ClaimedByUserId",
                table: "AssignedTasks");

            migrationBuilder.DropTable(
                name: "AssignedTaskAttachmentContents");

            migrationBuilder.DropTable(
                name: "AssignedTaskChecklistItems");

            migrationBuilder.DropTable(
                name: "AssignedTaskAttachments");

            migrationBuilder.DropIndex(
                name: "IX_AssignedTasks_ClaimedByUserId",
                table: "AssignedTasks");

            migrationBuilder.DropIndex(
                name: "IX_AssignedTasks_Status_DueDate",
                table: "AssignedTasks");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "AssignedTasks");

            migrationBuilder.DropColumn(
                name: "ClaimedByUserId",
                table: "AssignedTasks");

            migrationBuilder.DropColumn(
                name: "DueSoonNotifiedAt",
                table: "AssignedTasks");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAt",
                table: "AssignedTasks");
        }
    }
}
