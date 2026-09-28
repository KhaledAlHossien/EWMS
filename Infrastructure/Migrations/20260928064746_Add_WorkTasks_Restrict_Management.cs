using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_WorkTasks_Restrict_Management : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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

            // إدارة الهيكل للسوبر ادمن فقط (قرار المستخدم 2026-09-28): نسحب من رؤساء الفروع والأقسام
            // إدارة الفروع/الأقسام/المكاتب/الموظفين/الأدوار/أنواع الإجازات. ورئيس الفرع لا يقدّم إجازات.
            // (DbSeeder يضيف الصلاحيات فقط ولا يحذفها، لذلك يتم السحب هنا مرة واحدة)
            migrationBuilder.Sql(@"
DELETE rp FROM RolePermissions rp
INNER JOIN Roles r ON r.Id = rp.RoleId
INNER JOIN Permissions p ON p.Id = rp.PermissionId
WHERE r.Name IN (N'BranchManager', N'Manager')
  AND p.Name IN (N'ManageUsers', N'ManageBranches', N'ManageDepartments', N'ManageOffices',
                 N'ManageRoles', N'ManageVacations', N'ManageVacationTypes');

DELETE rp FROM RolePermissions rp
INNER JOIN Roles r ON r.Id = rp.RoleId
INNER JOIN Permissions p ON p.Id = rp.PermissionId
WHERE r.Name = N'BranchManager' AND p.Name = N'CreateVacation';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ملاحظة: الرجوع لا يعيد صلاحيات الإدارة للرؤساء — أعدها من صفحة الأدوار إن لزم
            migrationBuilder.DropTable(
                name: "UserWorkTasks");

            migrationBuilder.DropTable(
                name: "WorkTasks");
        }
    }
}
