using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_View_Permissions_For_Lookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // قراءة الأقسام / أنواع الإجازات / جداول الصيانة المساعدة كانت مفتوحة لأي مستخدم مسجّل،
            // وصارت بصلاحية "عرض" (قرار المستخدم 2026-09-30). حتى لا ينكسر ما يعمل اليوم تُمنح كل صلاحية
            // للأدوار التي تعتمد على تلك القراءة حالياً، ثم يسحبها السوبر ادمن ممن يشاء من صفحة الأدوار.

            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'ViewDepartments',        N'عرض الأقسام'),
    (N'ViewVacationTypes',      N'عرض أنواع الإجازات'),
    (N'ViewMaintenanceLookups', N'عرض أنواع الأجهزة والشركات والأعطال وحالات الطلب')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);");

            // الأقسام: كل الأدوار الحالية (قوائم الفرع/القسم في النماذج ولوحات التحكم تعتمد عليها)
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, np.Id
FROM Roles r
CROSS JOIN Permissions np
WHERE np.Name = N'ViewDepartments'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = np.Id);");

            // أنواع الإجازات: من يعرض أو يقدّم الإجازات. جداول الصيانة: من يتعامل مع طلبات الصيانة أو يدير جداولها
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions op ON op.Id = rp.PermissionId
INNER JOIN (VALUES
    (N'ViewVacations',            N'ViewVacationTypes'),
    (N'CreateVacation',           N'ViewVacationTypes'),
    (N'CreateVacationType',       N'ViewVacationTypes'),
    (N'EditVacationType',         N'ViewVacationTypes'),
    (N'DeleteVacationType',       N'ViewVacationTypes'),

    (N'ViewMaintenanceRequests',  N'ViewMaintenanceLookups'),
    (N'CreateMaintenanceRequest', N'ViewMaintenanceLookups'),
    (N'EditMaintenanceRequest',   N'ViewMaintenanceLookups'),
    (N'CreateMaintenanceLookup',  N'ViewMaintenanceLookups'),
    (N'EditMaintenanceLookup',    N'ViewMaintenanceLookups'),
    (N'DeleteMaintenanceLookup',  N'ViewMaintenanceLookups')
) m(HasName, NewName) ON m.HasName = op.Name
INNER JOIN Permissions np ON np.Name = m.NewName
WHERE NOT EXISTS (
    SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
