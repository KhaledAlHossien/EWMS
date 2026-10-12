using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Reports_Center : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportExportLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    ReportKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Filters = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExportLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportExportLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportLogs_BranchId_CreatedAt",
                table: "ReportExportLogs",
                columns: new[] { "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportLogs_CreatedAt",
                table: "ReportExportLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportLogs_UserId_CreatedAt",
                table: "ReportExportLogs",
                columns: new[] { "UserId", "CreatedAt" });

            // تصدير صفحتي التركيبات والمهام صار بصلاحية سحب (الأسماء والأوصاف تطابق AppPermissions): تُمنح لمن كان يصدّر،
            // أي كل دور يملك صلاحية عرض أجهزة (AnyDeviceView) أو ViewTaskBoard. بقية صلاحيات التقارير يضيفها DbSeeder
            // لمدير النظام فقط، وتُمنح للأدوار من صفحة الأدوار.
            migrationBuilder.Sql(@"
DECLARE @p TABLE (Name nvarchar(100), Description nvarchar(300));
INSERT INTO @p VALUES
 (N'ExportInstallationsReport', N'سحب تقرير التركيبات وتصديرها من صفحتها (التركيبات التي يراها، بلا كلمات السر)'),
 (N'ExportTasksReport', N'تصدير مهام لوحة المهام إلى Excel (المهام التي يراها في اللوحة)');

INSERT INTO Permissions (Name, Description)
SELECT p.Name, p.Description FROM @p p
WHERE NOT EXISTS (SELECT 1 FROM Permissions x WHERE x.Name = p.Name);

DECLARE @map TABLE (Source nvarchar(100), Target nvarchar(100));
INSERT INTO @map VALUES
 (N'ViewDevices', N'ExportInstallationsReport'), (N'ViewBranchDevices', N'ExportInstallationsReport'), (N'CreateDevice', N'ExportInstallationsReport'),
 (N'EditDevice', N'ExportInstallationsReport'), (N'DeleteDevice', N'ExportInstallationsReport'),
 (N'ViewTaskBoard', N'ExportTasksReport');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, t.Id
FROM RolePermissions rp
INNER JOIN Permissions s ON s.Id = rp.PermissionId
INNER JOIN @map m ON m.Source = s.Name
INNER JOIN Permissions t ON t.Name = m.Target
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = t.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // منح الأدوار يُحذف معها (Cascade)؛ التصديران يعودان بصلاحية العرض كما كانا بعد إرجاع الكود
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name IN (N'ExportInstallationsReport', N'ExportTasksReport');");

            migrationBuilder.DropTable(
                name: "ReportExportLogs");
        }
    }
}
