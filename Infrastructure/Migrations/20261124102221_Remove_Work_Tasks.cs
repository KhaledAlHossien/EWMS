using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Work_Tasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // أُزيلت ميزة مهام العمل (2026-11-24، قرار المستخدم): صلاحياتها (منحها في RolePermissions يُحذف بالـ cascade)
            // وإشعارات إسنادها (النوع 8 يبقى محجوزاً في NotificationType) ثم جدولاها.
            migrationBuilder.Sql(
                "DELETE FROM [Permissions] WHERE [Name] IN " +
                "(N'ViewWorkTasks', N'CreateWorkTask', N'EditWorkTask', N'DeleteWorkTask', N'ViewMyWorkTasks');");
            migrationBuilder.Sql("DELETE FROM [Notifications] WHERE [Type] = 8 OR [RelatedEntityType] = N'WorkTask';");

            migrationBuilder.DropTable(
                name: "UserWorkTasks");

            migrationBuilder.DropTable(
                name: "WorkTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // يعيد الجدولين فارغين فقط؛ الصلاحيات والإشعارات المحذوفة لا تُستعاد.
            migrationBuilder.CreateTable(
                name: "WorkTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkTasks_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkTasks",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    WorkTaskId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkTasks", x => new { x.UserId, x.WorkTaskId });
                    table.ForeignKey(
                        name: "FK_UserWorkTasks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserWorkTasks_WorkTasks_WorkTaskId",
                        column: x => x.WorkTaskId,
                        principalTable: "WorkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkTasks_WorkTaskId",
                table: "UserWorkTasks",
                column: "WorkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkTasks_BranchId_Name",
                table: "WorkTasks",
                columns: new[] { "BranchId", "Name" },
                unique: true);
        }
    }
}
