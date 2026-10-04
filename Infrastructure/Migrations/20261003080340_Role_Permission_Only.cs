using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Role_Permission_Only : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FirstApprovedByUserId",
                table: "Vacation",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacation_FirstApprovedByUserId",
                table: "Vacation",
                column: "FirstApprovedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vacation_Users_FirstApprovedByUserId",
                table: "Vacation",
                column: "FirstApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Role-Permission فقط (قرار المستخدم 2026-10-03): ما كان يُستنتج من المنصب (الاسم أو المكان)
            // صار صلاحيات صريحة. الـ seeder يضيف فقط، لذلك نضيفها هنا لنمنحها للأدوار القديمة بنفس سلوكها السابق،
            // ونقسم ApproveVacation إلى مرحلتين. الأسماء والأوصاف تطابق Application/Common/AppPermissions.

            // 1. الصلاحيات الجديدة
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'ViewDepartmentVacations', N'عرض إجازات موظفي قسمه وإحصائياتها'),
    (N'ViewBranchVacations',     N'عرض إجازات موظفي فرعه وإحصائياتها'),
    (N'ApproveVacationFirst',    N'الموافقة الأولى على إجازات موظفي فرعه أو رفضها'),
    (N'ApproveVacationFinal',    N'الاعتماد النهائي لإجازات موظفي فرعه أو رفضها'),
    (N'ViewBranchDashboard',     N'لوحة متابعة فرعه وأقسامه ومكاتبه'),
    (N'ViewDepartmentDashboard', N'لوحة متابعة قسمه ومكاتبه'),
    (N'ViewOfficeDashboard',     N'لوحة متابعة مكتبه'),
    (N'AssignTaskToDepartment',  N'إسناد مهام لأقسام فرعه'),
    (N'AssignTaskToOffice',      N'إسناد مهام لمكاتب قسمه'),
    (N'AssignTaskToUser',        N'إسناد مهام لموظفي مكتبه'),
    (N'HandleUnitTasks',         N'تولّي المهام المسندة لوحدته (تغيير حالتها وتفويضها)'),
    (N'ViewDepartmentMaintenance', N'الاطلاع على سجلات صيانة قسمه (وتعديلها وحذفها لمن يملك صلاحيتهما)')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

UPDATE Permissions SET Description = N'عرض إجازاتي' WHERE Name = N'ViewVacations';");

            // 2. ApproveVacation ← مرحلتان: Manager كان يوافق في الأولى، BranchManager في النهائي، وأي دور آخر في الاثنتين
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions op ON op.Id = rp.PermissionId AND op.Name = N'ApproveVacation'
INNER JOIN Roles r ON r.Id = rp.RoleId
INNER JOIN Permissions np ON np.Name IN (N'ApproveVacationFirst', N'ApproveVacationFinal')
WHERE NOT (r.Name = N'Manager' AND np.Name = N'ApproveVacationFinal')
  AND NOT (r.Name = N'BranchManager' AND np.Name = N'ApproveVacationFirst')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);

DELETE FROM Permissions WHERE Name = N'ApproveVacation';");

            // 3. ما كانت الأدوار القديمة تفعله بحكم اسمها صار صلاحيات صريحة (نفس السلوك تماماً)
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT r.Id, p.Id
FROM Roles r
INNER JOIN (VALUES
    (N'BranchManager', N'ViewBranchVacations'),
    (N'BranchManager', N'ViewBranchDashboard'),
    (N'BranchManager', N'AssignTaskToDepartment'),
    (N'Manager',       N'ViewDepartmentVacations'),
    (N'Manager',       N'ViewDepartmentDashboard'),
    (N'Manager',       N'AssignTaskToOffice'),
    (N'Manager',       N'HandleUnitTasks'),
    (N'Manager',       N'ViewDepartmentMaintenance'),
    (N'OfficeManager', N'ViewOfficeDashboard'),
    (N'OfficeManager', N'AssignTaskToUser'),
    (N'OfficeManager', N'HandleUnitTasks')
) v(RoleName, PermissionName) ON v.RoleName = r.Name
INNER JOIN Permissions p ON p.Name = v.PermissionName
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");

            // 4. صلاحيات تجربة "الخدمات المسندة للوحدات" (فرع test) لم تعد في النظام — إن وُجدت في قاعدة البيانات
            migrationBuilder.Sql(@"
DELETE FROM Permissions
WHERE Name LIKE N'Module.%'
   OR Name IN (N'ViewModuleAssignments', N'EditModuleAssignments', N'ViewAssignedTasks', N'CreateAssignedTask');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'ApproveVacation', N'الموافقة على الإجازات ورفضها'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'ApproveVacation');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions op ON op.Id = rp.PermissionId AND op.Name IN (N'ApproveVacationFirst', N'ApproveVacationFinal')
INNER JOIN Permissions np ON np.Name = N'ApproveVacation'
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);

DELETE FROM Permissions WHERE Name IN (
    N'ViewDepartmentVacations', N'ViewBranchVacations', N'ApproveVacationFirst', N'ApproveVacationFinal',
    N'ViewBranchDashboard', N'ViewDepartmentDashboard', N'ViewOfficeDashboard',
    N'AssignTaskToDepartment', N'AssignTaskToOffice', N'AssignTaskToUser', N'HandleUnitTasks',
    N'ViewDepartmentMaintenance');

UPDATE Permissions SET Description = N'عرض الإجازات' WHERE Name = N'ViewVacations';");

            migrationBuilder.DropForeignKey(
                name: "FK_Vacation_Users_FirstApprovedByUserId",
                table: "Vacation");

            migrationBuilder.DropIndex(
                name: "IX_Vacation_FirstApprovedByUserId",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "FirstApprovedByUserId",
                table: "Vacation");
        }
    }
}
