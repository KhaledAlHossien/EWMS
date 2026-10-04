using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// بيانات الجهاز (النوع، الشركة، الموديل، الرقم التسلسلي) تخرج من طلب الصيانة إلى جدول DeviceMaintenances
    /// برقم تسلسلي فريد، والطلب يشير إلى جهاز — قرار المستخدم 2026-10-04.
    /// عُدّلت يدوياً: EF ولّد RenameColumn(DeviceTypeId → DeviceMaintenanceId) وحذف الأعمدة قبل نقل بياناتها.
    /// هنا: الجدول أولاً، ثم جهاز لكل رقم تسلسلي موجود (الطلبات بنفس الرقم = جهاز واحد، بيانات أحدث طلب)،
    /// ثم ربط الطلبات، ثم حذف الأعمدة القديمة.
    /// </summary>
    public partial class Add_DeviceMaintenance : Migration
    {
        // الرقم التسلسلي المعتمد لطلب قديم: بلا فراغات، والفارغ يُعطى رقماً مؤقتاً فريداً
        private const string SerialOfRequest =
            "CASE WHEN LTRIM(RTRIM(r.SerialNumber)) = N'' THEN CONCAT(N'NOSN-', r.Id) ELSE LTRIM(RTRIM(r.SerialNumber)) END";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. جدول الأجهزة
            migrationBuilder.CreateTable(
                name: "DeviceMaintenances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DeviceTypeId = table.Column<int>(type: "int", nullable: false),
                    DeviceCompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceMaintenances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceMaintenances_DeviceCompanies_DeviceCompanyId",
                        column: x => x.DeviceCompanyId,
                        principalTable: "DeviceCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceMaintenances_DeviceTypes_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalTable: "DeviceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMaintenances_DeviceCompanyId",
                table: "DeviceMaintenances",
                column: "DeviceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMaintenances_DeviceTypeId",
                table: "DeviceMaintenances",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMaintenances_Model",
                table: "DeviceMaintenances",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceMaintenances_SerialNumber",
                table: "DeviceMaintenances",
                column: "SerialNumber",
                unique: true);

            // 2. جهاز لكل رقم تسلسلي في الطلبات الحالية (بيانات أحدث طلب له)
            migrationBuilder.Sql($@"
INSERT INTO DeviceMaintenances (Name, SerialNumber, Model, Description, DeviceTypeId, DeviceCompanyId)
SELECT N'', s.Serial, last.Model, N'', last.DeviceTypeId, last.DeviceCompanyId
FROM (
    SELECT x.Serial, MAX(x.Id) AS LastId
    FROM (SELECT r.Id, {SerialOfRequest} AS Serial FROM MaintenanceRequests r) x
    GROUP BY x.Serial
) s
INNER JOIN MaintenanceRequests last ON last.Id = s.LastId;");

            // 3. ربط كل طلب بجهازه
            migrationBuilder.AddColumn<int>(
                name: "DeviceMaintenanceId",
                table: "MaintenanceRequests",
                type: "int",
                nullable: true);

            migrationBuilder.Sql($@"
UPDATE r SET DeviceMaintenanceId = d.Id
FROM MaintenanceRequests r
INNER JOIN DeviceMaintenances d ON d.SerialNumber = {SerialOfRequest};");

            migrationBuilder.AlterColumn<int>(
                name: "DeviceMaintenanceId",
                table: "MaintenanceRequests",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 4. حذف أعمدة الجهاز من الطلب بعد نقلها
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_DeviceCompanies_DeviceCompanyId",
                table: "MaintenanceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_DeviceTypes_DeviceTypeId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_DeviceCompanyId_CreatedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_DeviceTypeId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_Model",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_SerialNumber",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(name: "DeviceTypeId", table: "MaintenanceRequests");
            migrationBuilder.DropColumn(name: "DeviceCompanyId", table: "MaintenanceRequests");
            migrationBuilder.DropColumn(name: "Model", table: "MaintenanceRequests");
            migrationBuilder.DropColumn(name: "SerialNumber", table: "MaintenanceRequests");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_DeviceMaintenanceId_CreatedAt",
                table: "MaintenanceRequests",
                columns: new[] { "DeviceMaintenanceId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_DeviceMaintenances_DeviceMaintenanceId",
                table: "MaintenanceRequests",
                column: "DeviceMaintenanceId",
                principalTable: "DeviceMaintenances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 5. صلاحيات الأجهزة (تطابق AppPermissions). الطلب صار يُربط بجهاز، فمن يقدّم/يعدّل طلباً
            //    يحتاج عرض الأجهزة وإضافتها/تعديلها حتى لا يتوقف عمله. الحذف لا يُمنح إلا للسوبر ادمن (الـ seeder).
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
    (N'ViewMaintenanceDevices',  N'عرض أجهزة الصيانة والبحث فيها بالرقم التسلسلي'),
    (N'CreateMaintenanceDevice', N'إضافة جهاز صيانة'),
    (N'EditMaintenanceDevice',   N'تعديل بيانات جهاز صيانة'),
    (N'DeleteMaintenanceDevice', N'حذف جهاز صيانة (إن لم تكن له طلبات)')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions op ON op.Id = rp.PermissionId
INNER JOIN (VALUES
    (N'ViewMaintenanceRequests',  N'ViewMaintenanceDevices'),
    (N'CreateMaintenanceRequest', N'ViewMaintenanceDevices'),
    (N'CreateMaintenanceRequest', N'CreateMaintenanceDevice'),
    (N'EditMaintenanceRequest',   N'ViewMaintenanceDevices'),
    (N'EditMaintenanceRequest',   N'EditMaintenanceDevice')
) m(HasName, NewName) ON m.HasName = op.Name
INNER JOIN Permissions np ON np.Name = m.NewName
WHERE NOT EXISTS (
    SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM Permissions WHERE Name IN (
    N'ViewMaintenanceDevices', N'CreateMaintenanceDevice', N'EditMaintenanceDevice', N'DeleteMaintenanceDevice');");

            // أعمدة الجهاز تعود للطلب ببيانات جهازه
            migrationBuilder.AddColumn<int>(name: "DeviceTypeId", table: "MaintenanceRequests", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "DeviceCompanyId", table: "MaintenanceRequests", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Model", table: "MaintenanceRequests", type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "SerialNumber", table: "MaintenanceRequests", type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE r SET DeviceTypeId = d.DeviceTypeId, DeviceCompanyId = d.DeviceCompanyId, Model = d.Model,
             SerialNumber = CASE WHEN d.SerialNumber LIKE N'NOSN-%' THEN N'' ELSE d.SerialNumber END
FROM MaintenanceRequests r
INNER JOIN DeviceMaintenances d ON d.Id = r.DeviceMaintenanceId;");

            migrationBuilder.AlterColumn<int>(name: "DeviceTypeId", table: "MaintenanceRequests", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "DeviceCompanyId", table: "MaintenanceRequests", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_DeviceMaintenances_DeviceMaintenanceId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_DeviceMaintenanceId_CreatedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(name: "DeviceMaintenanceId", table: "MaintenanceRequests");

            migrationBuilder.DropTable(name: "DeviceMaintenances");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_DeviceCompanyId_CreatedAt",
                table: "MaintenanceRequests",
                columns: new[] { "DeviceCompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_DeviceTypeId",
                table: "MaintenanceRequests",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_Model",
                table: "MaintenanceRequests",
                column: "Model");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_SerialNumber",
                table: "MaintenanceRequests",
                column: "SerialNumber");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_DeviceCompanies_DeviceCompanyId",
                table: "MaintenanceRequests",
                column: "DeviceCompanyId",
                principalTable: "DeviceCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_DeviceTypes_DeviceTypeId",
                table: "MaintenanceRequests",
                column: "DeviceTypeId",
                principalTable: "DeviceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
