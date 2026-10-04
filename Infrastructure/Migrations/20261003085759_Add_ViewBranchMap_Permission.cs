using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_ViewBranchMap_Permission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // خريطة الفرع لكل رئيس فرع ببيانات فرعه (2026-10-03): صلاحية جديدة تُمنح للدور القديم BranchManager
            // (السوبر ادمن يحصل عليها من الـ seeder). الاسم والوصف يطابقان AppPermissions.
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'ViewBranchMap', N'عرض خريطة سوريا ببيانات فرعه في لوحة المتابعة'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'ViewBranchMap');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
INNER JOIN Permissions p ON p.Name = N'ViewBranchMap'
WHERE r.Name = N'BranchManager'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'ViewBranchMap';");
        }
    }
}
