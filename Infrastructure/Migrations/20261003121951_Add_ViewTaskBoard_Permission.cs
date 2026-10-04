using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_ViewTaskBoard_Permission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // لوحة المهام لم تعد مفتوحة لكل مستخدم (2026-10-03): صلاحية ViewTaskBoard تفتحها.
            // نمنحها لمن كان يستعملها فعلاً حتى لا يفقد أحد وصوله: كل دور يملك صلاحية إسناد/تولٍّ
            // (AssignTaskTo*/HandleUnitTasks)، والأدوار القديمة المسمّاة التي كانت تصل إليها ضمناً.
            // الأدوار التي أنشأها المستخدم ولا تملك صلاحيات المهام لا تحصل عليها. (السوبر ادمن من الـ seeder.)
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'ViewTaskBoard', N'الوصول إلى لوحة المهام واستقبال المهام المسندة إليه شخصياً'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'ViewTaskBoard');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT r.Id, vp.Id
FROM Roles r
INNER JOIN Permissions vp ON vp.Name = N'ViewTaskBoard'
WHERE (r.Name IN (N'Emp', N'OfficeManager', N'Manager', N'BranchManager')
       OR EXISTS (SELECT 1 FROM RolePermissions rp
                  INNER JOIN Permissions p ON p.Id = rp.PermissionId
                  WHERE rp.RoleId = r.Id
                    AND p.Name IN (N'AssignTaskToDepartment', N'AssignTaskToOffice', N'AssignTaskToUser', N'HandleUnitTasks')))
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = vp.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'ViewTaskBoard';");
        }
    }
}
