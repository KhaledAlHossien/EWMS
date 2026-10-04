using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Vacation_Print : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RejectedAtStage",
                table: "Vacation",
                type: "int",
                nullable: true);

            // الطلبات المرفوضة سابقاً: من وافق عليها في المرحلة الأولى رُفضت في النهائية (3)، وإلا في الأولى (1).
            // (طلب تجاوز المرحلة الأولى ثم رُفض قديماً يُحسب "الأولى" — لا يمكن تمييزه، يؤثر على نص الطباعة فقط)
            migrationBuilder.Sql(@"
UPDATE Vacation SET RejectedAtStage = CASE WHEN ManagerAccept = 1 THEN 3 ELSE 1 END
WHERE Status = 5 AND RejectedAtStage IS NULL;");

            // صلاحية الطباعة الجديدة: تُمنح لكل دور يستطيع عرض إجازات (نفس حدّ العرض)، ويعدّلها المستخدم من صفحة الأدوار.
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'PrintVacation', N'طباعة نموذج طلب الإجازة (للإجازات التي يستطيع عرضها)'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'PrintVacation');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
CROSS JOIN (SELECT Id FROM Permissions WHERE Name = N'PrintVacation') np
WHERE p.Name IN (N'ViewVacations', N'ViewDepartmentVacations', N'ViewBranchVacations', N'ApproveVacationFirst', N'ApproveVacationFinal')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'PrintVacation';");

            migrationBuilder.DropColumn(
                name: "RejectedAtStage",
                table: "Vacation");
        }
    }
}
