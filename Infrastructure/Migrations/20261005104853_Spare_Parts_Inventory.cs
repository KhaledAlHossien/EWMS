using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Spare_Parts_Inventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReplacementCostThreshold",
                table: "DeviceTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpareParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinQuantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpareParts", x => x.Id);
                    table.CheckConstraint("CK_SpareParts_Quantity", "[Quantity] >= 0");
                    table.ForeignKey(
                        name: "FK_SpareParts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRequestParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: false),
                    SparePartId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IssuedById = table.Column<int>(type: "int", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRequestParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequestParts_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequestParts_SpareParts_SparePartId",
                        column: x => x.SparePartId,
                        principalTable: "SpareParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequestParts_Users_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SparePartDeviceCompanies",
                columns: table => new
                {
                    SparePartId = table.Column<int>(type: "int", nullable: false),
                    DeviceCompanyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparePartDeviceCompanies", x => new { x.SparePartId, x.DeviceCompanyId });
                    table.ForeignKey(
                        name: "FK_SparePartDeviceCompanies_DeviceCompanies_DeviceCompanyId",
                        column: x => x.DeviceCompanyId,
                        principalTable: "DeviceCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SparePartDeviceCompanies_SpareParts_SparePartId",
                        column: x => x.SparePartId,
                        principalTable: "SpareParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SparePartDeviceTypes",
                columns: table => new
                {
                    SparePartId = table.Column<int>(type: "int", nullable: false),
                    DeviceTypeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparePartDeviceTypes", x => new { x.SparePartId, x.DeviceTypeId });
                    table.ForeignKey(
                        name: "FK_SparePartDeviceTypes_DeviceTypes_DeviceTypeId",
                        column: x => x.DeviceTypeId,
                        principalTable: "DeviceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SparePartDeviceTypes_SpareParts_SparePartId",
                        column: x => x.SparePartId,
                        principalTable: "SpareParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SparePartMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SparePartId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparePartMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SparePartMovements_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SparePartMovements_SpareParts_SparePartId",
                        column: x => x.SparePartId,
                        principalTable: "SpareParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SparePartMovements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequestParts_IssuedById",
                table: "MaintenanceRequestParts",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequestParts_MaintenanceRequestId",
                table: "MaintenanceRequestParts",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequestParts_SparePartId_IssuedAt",
                table: "MaintenanceRequestParts",
                columns: new[] { "SparePartId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SparePartDeviceCompanies_DeviceCompanyId",
                table: "SparePartDeviceCompanies",
                column: "DeviceCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SparePartDeviceTypes_DeviceTypeId",
                table: "SparePartDeviceTypes",
                column: "DeviceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SparePartMovements_MaintenanceRequestId",
                table: "SparePartMovements",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SparePartMovements_SparePartId_CreatedAt",
                table: "SparePartMovements",
                columns: new[] { "SparePartId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SparePartMovements_Type_Date",
                table: "SparePartMovements",
                columns: new[] { "Type", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_SparePartMovements_UserId",
                table: "SparePartMovements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SpareParts_DepartmentId_Name",
                table: "SpareParts",
                columns: new[] { "DepartmentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpareParts_PartNumber",
                table: "SpareParts",
                column: "PartNumber");

            // صلاحيات المخزون (الـ seeder يضيفها أيضاً، لكن المنح يحتاج الصفوف الآن)
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description FROM (VALUES
    (N'ViewSpareParts',       N'عرض قطع غيار مخزون قسمه وحركاتها'),
    (N'CreateSparePart',      N'إضافة قطعة غيار إلى مخزون قسمه'),
    (N'EditSparePart',        N'تعديل بيانات قطعة غيار في مخزون قسمه'),
    (N'DeleteSparePart',      N'حذف قطعة غيار من مخزون قسمه (إن لم تكن لها حركات)'),
    (N'ReceiveSpareParts',    N'إدخال قطع غيار إلى مخزون قسمه (ويصله تنبيه نزول القطعة تحت حدها الأدنى)'),
    (N'AdjustSparePartStock', N'تسوية رصيد قطعة غيار في مخزون قسمه (جرد أو تالف)'),
    (N'IssueSparePart',       N'صرف قطع غيار على طلبات الصيانة وإعادتها (طلباته، وطلبات قسمه لمن يملك الاطلاع على القسم)'),
    (N'ViewSparePartReports', N'تقارير قطع غيار قسمه: أكثر القطع صرفاً وتكلفة الأجهزة')
) v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

-- الفنيون (من يسجّل أو يعدّل طلبات الصيانة): عرض المخزون والصرف على طلباتهم
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
CROSS JOIN (SELECT Id FROM Permissions WHERE Name IN (N'ViewSpareParts', N'IssueSparePart')) np
WHERE p.Name IN (N'CreateMaintenanceRequest', N'EditMaintenanceRequest')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);

-- رئيس القسم (من يملك نقل طلبات القسم): إدارة المخزون كاملة وتقاريره
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
CROSS JOIN (SELECT Id FROM Permissions WHERE Name IN (N'ViewSpareParts', N'CreateSparePart', N'EditSparePart', N'DeleteSparePart',
    N'ReceiveSpareParts', N'AdjustSparePartStock', N'ViewSparePartReports')) np
WHERE p.Name = N'AssignMaintenanceRequest'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name IN (N'ViewSpareParts', N'CreateSparePart', N'EditSparePart', N'DeleteSparePart',
    N'ReceiveSpareParts', N'AdjustSparePartStock', N'IssueSparePart', N'ViewSparePartReports');");

            migrationBuilder.DropTable(
                name: "MaintenanceRequestParts");

            migrationBuilder.DropTable(
                name: "SparePartDeviceCompanies");

            migrationBuilder.DropTable(
                name: "SparePartDeviceTypes");

            migrationBuilder.DropTable(
                name: "SparePartMovements");

            migrationBuilder.DropTable(
                name: "SpareParts");

            migrationBuilder.DropColumn(
                name: "ReplacementCostThreshold",
                table: "DeviceTypes");
        }
    }
}
