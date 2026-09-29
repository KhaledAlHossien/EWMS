using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Split_Maintenance_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // صلاحية منفصلة لكل عملية في الصيانة (قرار المستخدم 2026-09-29).
            // الـ seeder يضيف فقط، لذلك: نضيف الصلاحيات الجديدة هنا، وننقل إليها كل دور كان يملك القديمة،
            // ثم نحذف القديمة (RolePermissions تُحذف معها بالـ Cascade).

            // 1. الصلاحيات الجديدة
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'ViewMaintenanceTasks',     N'عرض مهام الصيانة'),
    (N'CreateMaintenanceTask',    N'إضافة مهمة صيانة'),
    (N'EditMaintenanceTask',      N'تعديل مهمة صيانة'),
    (N'DeleteMaintenanceTask',    N'حذف مهمة صيانة'),
    (N'DeleteMaintenanceRequest', N'حذف طلب صيانة'),
    (N'CreateMaintenanceLookup',  N'إضافة أنواع الأجهزة والشركات والأعطال وحالات الطلب'),
    (N'EditMaintenanceLookup',    N'تعديل أنواع الأجهزة والشركات والأعطال وحالات الطلب'),
    (N'DeleteMaintenanceLookup',  N'حذف أنواع الأجهزة والشركات والأعطال وحالات الطلب')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

UPDATE Permissions SET Description = N'تعديل طلب صيانة' WHERE Name = N'EditMaintenanceRequest';");

            // 2. من كان يملك القديمة يحصل على ما يقابلها (EditMaintenanceRequest كانت تشمل الحذف)
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions op ON op.Id = rp.PermissionId
INNER JOIN (VALUES
    (N'ManageMaintenanceTasks',   N'ViewMaintenanceTasks'),
    (N'ManageMaintenanceTasks',   N'CreateMaintenanceTask'),
    (N'ManageMaintenanceTasks',   N'EditMaintenanceTask'),
    (N'ManageMaintenanceTasks',   N'DeleteMaintenanceTask'),
    (N'EditMaintenanceRequest',   N'DeleteMaintenanceRequest'),
    (N'ManageMaintenanceLookups', N'CreateMaintenanceLookup'),
    (N'ManageMaintenanceLookups', N'EditMaintenanceLookup'),
    (N'ManageMaintenanceLookups', N'DeleteMaintenanceLookup')
) m(OldName, NewName) ON m.OldName = op.Name
INNER JOIN Permissions np ON np.Name = m.NewName
WHERE NOT EXISTS (
    SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");

            // 3. حذف الصلاحيات المجمَّعة القديمة
            migrationBuilder.Sql(@"
DELETE FROM Permissions WHERE Name IN (N'ManageMaintenanceTasks', N'ManageMaintenanceLookups');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
