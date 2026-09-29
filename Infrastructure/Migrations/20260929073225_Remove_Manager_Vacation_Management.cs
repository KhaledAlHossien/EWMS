using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Manager_Vacation_Management : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // إدارة الإجازات وأنواعها للسوبر ادمن فقط (قرار المستخدم 2026-09-28) — أُعيدتا لرئيس القسم
            // خطأً عند حل تعارض دمج في DbSeeder، والـ seeder المُصحَّح لم يعد يضيفهما
            migrationBuilder.Sql(@"
DELETE rp FROM RolePermissions rp
INNER JOIN Roles r ON r.Id = rp.RoleId
INNER JOIN Permissions p ON p.Id = rp.PermissionId
WHERE r.Name = N'Manager' AND p.Name IN (N'ManageVacations', N'ManageVacationTypes');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
