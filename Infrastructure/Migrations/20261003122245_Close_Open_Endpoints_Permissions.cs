using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Close_Open_Endpoints_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // لا شيء مفتوح لأي مستخدم مسجّل بلا صلاحية (قرار المستخدم 2026-10-03). أربع صلاحيات جديدة لما كان مفتوحاً:
            //   ViewOrganizationDashboard (نظرة عامة على المؤسسة — كانت لاسم الدور SuperAdmin)،
            //   ViewMyDashboard (لوحتي الشخصية)، ViewMyWorkTasks (مهام العمل المسندة لي)، ViewNotifications (الإشعارات).
            // السوبر ادمن يحصل عليها من الـ seeder. نمنح الثلاث الشخصية للأدوار القديمة المسمّاة فقط (كانت تستعملها ضمناً)؛
            // الأدوار التي أنشأها المستخدم لا تحصل على شيء تلقائياً — يمنحها من صفحة الأدوار.
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'ViewOrganizationDashboard', N'لوحة نظرة عامة على المؤسسة كلها (كل الفروع)'),
    (N'ViewMyDashboard',           N'لوحتي الشخصية في لوحة المتابعة'),
    (N'ViewMyWorkTasks',           N'عرض مهام العمل المسندة إليه وصفحاتها'),
    (N'ViewNotifications',         N'عرض الإشعارات واستلامها (الجرس والتنبيهات اللحظية)')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
INNER JOIN Permissions p ON p.Name IN (N'ViewMyDashboard', N'ViewMyWorkTasks', N'ViewNotifications')
WHERE r.Name IN (N'Emp', N'OfficeManager', N'Manager', N'BranchManager')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name IN (N'ViewOrganizationDashboard', N'ViewMyDashboard', N'ViewMyWorkTasks', N'ViewNotifications');");
        }
    }
}
