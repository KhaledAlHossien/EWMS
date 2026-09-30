using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Split_All_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // صلاحية منفصلة لكل عملية في كل النظام (قرار المستخدم 2026-09-30).
            // الـ seeder يضيف فقط، لذلك: نضيف الصلاحيات المفصّلة هنا، وننقل إليها كل دور كان يملك
            // صلاحية "Manage..." المجمَّعة (حتى لا يفقد أحد ما كان يستطيعه)، ثم نحذف المجمَّعة
            // (RolePermissions تُحذف معها بالـ Cascade). الأسماء والأوصاف تطابق Application/Common/AppPermissions.

            // 1. الصلاحيات الجديدة
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'ViewUsers',   N'عرض المستخدمين'),
    (N'CreateUser',  N'إضافة مستخدم'),
    (N'EditUser',    N'تعديل مستخدم'),
    (N'DeleteUser',  N'حذف مستخدم'),

    (N'ViewBranches', N'عرض الفروع'),
    (N'CreateBranch', N'إضافة فرع'),
    (N'EditBranch',   N'تعديل فرع'),
    (N'DeleteBranch', N'حذف فرع'),

    (N'CreateDepartment', N'إضافة قسم'),
    (N'EditDepartment',   N'تعديل قسم'),
    (N'DeleteDepartment', N'حذف قسم'),

    (N'ViewOffices',  N'عرض المكاتب'),
    (N'CreateOffice', N'إضافة مكتب'),
    (N'EditOffice',   N'تعديل مكتب'),
    (N'DeleteOffice', N'حذف مكتب'),

    (N'ViewRoles',  N'عرض الأدوار والصلاحيات'),
    (N'CreateRole', N'إضافة دور'),
    (N'EditRole',   N'تعديل دور وصلاحياته'),
    (N'DeleteRole', N'حذف دور'),

    (N'CancelVacation', N'إلغاء طلب إجازة'),

    (N'CreateVacationType', N'إضافة نوع إجازة'),
    (N'EditVacationType',   N'تعديل نوع إجازة'),
    (N'DeleteVacationType', N'حذف نوع إجازة'),

    (N'ViewWorkTasks',  N'عرض مهام العمل وإسنادها'),
    (N'CreateWorkTask', N'إضافة مهمة عمل'),
    (N'EditWorkTask',   N'تعديل مهمة عمل وإسنادها'),
    (N'DeleteWorkTask', N'حذف مهمة عمل'),

    (N'CreateDevice', N'إضافة مناطق ومواقع وأجهزة'),
    (N'EditDevice',   N'تعديل المناطق والمواقع والأجهزة'),
    (N'DeleteDevice', N'حذف المناطق والمواقع والأجهزة')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

UPDATE Permissions SET Description = N'الموافقة على الإجازات ورفضها' WHERE Name = N'ApproveVacation';");

            // 2. من كان يملك المجمَّعة يحصل على ما يقابلها
            //    (CreateVacation كانت تشمل إلغاء الطلب، و ManageDevices كانت تشمل المشاهدة)
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions op ON op.Id = rp.PermissionId
INNER JOIN (VALUES
    (N'ManageUsers', N'ViewUsers'), (N'ManageUsers', N'CreateUser'),
    (N'ManageUsers', N'EditUser'),  (N'ManageUsers', N'DeleteUser'),

    (N'ManageBranches', N'ViewBranches'), (N'ManageBranches', N'CreateBranch'),
    (N'ManageBranches', N'EditBranch'),   (N'ManageBranches', N'DeleteBranch'),

    (N'ManageDepartments', N'CreateDepartment'), (N'ManageDepartments', N'EditDepartment'),
    (N'ManageDepartments', N'DeleteDepartment'),

    (N'ManageOffices', N'ViewOffices'), (N'ManageOffices', N'CreateOffice'),
    (N'ManageOffices', N'EditOffice'),  (N'ManageOffices', N'DeleteOffice'),

    (N'ManageRoles', N'ViewRoles'), (N'ManageRoles', N'CreateRole'),
    (N'ManageRoles', N'EditRole'),  (N'ManageRoles', N'DeleteRole'),

    (N'CreateVacation', N'CancelVacation'),

    (N'ManageVacationTypes', N'CreateVacationType'), (N'ManageVacationTypes', N'EditVacationType'),
    (N'ManageVacationTypes', N'DeleteVacationType'),

    (N'ManageWorkTasks', N'ViewWorkTasks'), (N'ManageWorkTasks', N'CreateWorkTask'),
    (N'ManageWorkTasks', N'EditWorkTask'),  (N'ManageWorkTasks', N'DeleteWorkTask'),

    (N'ManageDevices', N'ViewDevices'), (N'ManageDevices', N'CreateDevice'),
    (N'ManageDevices', N'EditDevice'),  (N'ManageDevices', N'DeleteDevice')
) m(OldName, NewName) ON m.OldName = op.Name
INNER JOIN Permissions np ON np.Name = m.NewName
WHERE NOT EXISTS (
    SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");

            // 3. حذف المجمَّعة القديمة (ManageVacations لم تكن تحمي أي endpoint)
            migrationBuilder.Sql(@"
DELETE FROM Permissions WHERE Name IN (
    N'ManageUsers', N'ManageBranches', N'ManageDepartments', N'ManageOffices', N'ManageRoles',
    N'ManageVacations', N'ManageVacationTypes', N'ManageWorkTasks', N'ManageDevices');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
