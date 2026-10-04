using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Split_Status_Stats_Toggle_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // تفصيل ثلاث صلاحيات (قرار المستخدم 2026-10-03). الـ seeder يضيف فقط، لذلك نضيفها هنا
            // وننقل لكل دور كان يملك الصلاحية الأوسع ما يقابلها حتى لا يفقد أحد ما كان يستطيعه:
            //   تغيير حالة الطلب   ← من يملك EditMaintenanceRequest
            //   إحصائيات الصيانة   ← من يملك ViewMaintenanceRequests أو ViewMaintenanceTasks
            //   تفعيل/تعطيل حساب   ← من يملك EditUser
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'ChangeMaintenanceStatus', N'تغيير حالة طلب صيانة (دون تعديل بياناته)'),
    (N'ViewMaintenanceStats',    N'عرض إحصائيات الصيانة (طلبات ومهام: سجلاته وسجلات قسمه لمن يملك الاطلاع على القسم)'),
    (N'ToggleUserActive',        N'تفعيل حسابات الموظفين وتعطيلها (دون تعديل بياناتهم)')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

UPDATE Permissions SET Description = N'تعديل بيانات طلب صيانة (وحالته عند التعديل تحتاج صلاحية تغيير الحالة)' WHERE Name = N'EditMaintenanceRequest';

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions op ON op.Id = rp.PermissionId
INNER JOIN (VALUES
    (N'EditMaintenanceRequest',  N'ChangeMaintenanceStatus'),
    (N'ViewMaintenanceRequests', N'ViewMaintenanceStats'),
    (N'ViewMaintenanceTasks',    N'ViewMaintenanceStats'),
    (N'EditUser',                N'ToggleUserActive')
) m(OldName, NewName) ON m.OldName = op.Name
INNER JOIN Permissions np ON np.Name = m.NewName
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name IN (N'ChangeMaintenanceStatus', N'ViewMaintenanceStats', N'ToggleUserActive');");
        }
    }
}
