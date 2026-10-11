using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sites_Permissions_Installation_Units : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "DeviceSites",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "DeviceSites",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_BranchId",
                table: "DeviceSites",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_DepartmentId",
                table: "DeviceSites",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSites_Branches_BranchId",
                table: "DeviceSites",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceSites_Departments_DepartmentId",
                table: "DeviceSites",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // صلاحيات المواقع المستقلة وViewBranchDevices (الأسماء والأوصاف تطابق AppPermissions). المنح يحفظ ما كان:
            // صلاحيات الأجهزة كانت تشمل المواقع وتعرض كل التركيبات → ViewSites/CreateSite/EditSite/DeleteSite بمقابلها،
            // وViewBranchDevices لكل دور يملك أي صلاحية أجهزة (يبقى يرى تركيبات فرعه كلها). الوحدة المالكة للتركيبات
            // القديمة يملؤها DbSeeder.BackfillInstallationUnitsAsync عند الإقلاع (من سجل التغييرات).
            migrationBuilder.Sql(@"
DECLARE @p TABLE (Name nvarchar(100), Description nvarchar(300));
INSERT INTO @p VALUES
 (N'ViewSites',  N'عرض سجل المواقع المشترك (كل المواقع)'),
 (N'CreateSite', N'إضافة موقع إلى السجل المشترك'),
 (N'EditSite',   N'تعديل بيانات أي موقع (الاسم والإحداثيات تظهر لكل الفروع)'),
 (N'DeleteSite', N'حذف موقع (إن لم يكن عليه تركيبات أو بيانات لأي فرع)'),
 (N'ViewBranchDevices', N'عرض تركيبات الأجهزة في فرعي كله وإدارتها ضمن صلاحياتي (لا قسمي فقط)');

INSERT INTO Permissions (Name, Description)
SELECT p.Name, p.Description FROM @p p
WHERE NOT EXISTS (SELECT 1 FROM Permissions x WHERE x.Name = p.Name);

DECLARE @map TABLE (Source nvarchar(100), Target nvarchar(100));
INSERT INTO @map VALUES
 (N'ViewDevices', N'ViewSites'), (N'CreateDevice', N'ViewSites'), (N'EditDevice', N'ViewSites'), (N'DeleteDevice', N'ViewSites'),
 (N'CreateDevice', N'CreateSite'), (N'EditDevice', N'EditSite'), (N'DeleteDevice', N'DeleteSite'),
 (N'ViewDevices', N'ViewBranchDevices'), (N'CreateDevice', N'ViewBranchDevices'), (N'EditDevice', N'ViewBranchDevices'), (N'DeleteDevice', N'ViewBranchDevices');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, t.Id
FROM RolePermissions rp
INNER JOIN Permissions s ON s.Id = rp.PermissionId
INNER JOIN @map m ON m.Source = s.Name
INNER JOIN Permissions t ON t.Name = m.Target
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = t.Id);

UPDATE Permissions SET Description = N'عرض كتالوج الأجهزة وتركيبات قسمي وسجلها (بلا كلمات السر)' WHERE Name = N'ViewDevices';
UPDATE Permissions SET Description = N'إضافة أجهزة للكتالوج وتركيبات (تُسجَّل باسم قسمي) واستيرادها من Excel' WHERE Name = N'CreateDevice';
UPDATE Permissions SET Description = N'تعديل الأجهزة والتركيبات التي أراها' WHERE Name = N'EditDevice';
UPDATE Permissions SET Description = N'حذف الأجهزة والتركيبات التي أراها' WHERE Name = N'DeleteDevice';
UPDATE Permissions SET Description = N'إظهار كلمات سر التركيبات التي أراها ونسخها (يُسجَّل كل إظهار)' WHERE Name = N'RevealDevicePasswords';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // منح الأدوار يُحذف معها (Cascade)؛ صلاحيات الأجهزة تعود تشمل المواقع كما كانت
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name IN (N'ViewSites', N'CreateSite', N'EditSite', N'DeleteSite', N'ViewBranchDevices');");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSites_Branches_BranchId",
                table: "DeviceSites");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceSites_Departments_DepartmentId",
                table: "DeviceSites");

            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_BranchId",
                table: "DeviceSites");

            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_DepartmentId",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "DeviceSites");
        }
    }
}
