using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Maintenance_Extension_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // نقل ميزات الصيانة الموسّعة من فرع test (2026-10-03). جدولا سجل الطلب والتوقيع يُنشئهما
            // Add_Maintenance_Activity_Signature المنقولة بنفس معرّفها (قاعدة بيانات طبّقتها من قبل تتخطاها)،
            // وهذه تضيف الصلاحيات الخاصة بها فقط، وتمنح الدور القديم Manager ما كان رئيس القسم يفعله
            // (نقل الطلبات والمهام داخل قسمه + التوقيع على ورقة التسليم). الأسماء تطابق AppPermissions.
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'AssignMaintenanceTask',    N'توجيه مهمة صيانة أو نقلها إلى موظف من قسمه'),
    (N'AssignMaintenanceRequest', N'نقل طلب صيانة من قسمه إلى موظف آخر من قسمه'),
    (N'SignMaintenanceReceipt',   N'توقيع أوراق تسليم طلبات صيانة قسمه (اسمه وتوقيعه في الطباعة)')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

UPDATE Permissions SET Description = N'توجيه مهمة صيانة أو نقلها إلى موظف من قسمه' WHERE Name = N'AssignMaintenanceTask';
UPDATE Permissions SET Description = N'نقل طلب صيانة من قسمه إلى موظف آخر من قسمه' WHERE Name = N'AssignMaintenanceRequest';

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
INNER JOIN Permissions p ON p.Name IN (N'AssignMaintenanceTask', N'AssignMaintenanceRequest', N'SignMaintenanceReceipt')
WHERE r.Name = N'Manager'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'SignMaintenanceReceipt';");
        }
    }
}
