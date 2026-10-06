using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Device_Inventory_Review : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_SiteId",
                table: "DeviceSites");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Sites",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Sites",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Sites",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Sites",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleParty",
                table: "Sites",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Sites",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "DeviceSites",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "SubnetMask",
                table: "DeviceSites",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Pass",
                table: "DeviceSites",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "DeviceSites",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Ip",
                table: "DeviceSites",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Firmware",
                table: "DeviceSites",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "DeviceSites",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "InstallDate",
                table: "DeviceSites",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVerifiedAt",
                table: "DeviceSites",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MacAddress",
                table: "DeviceSites",
                type: "nvarchar(17)",
                maxLength: 17,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Port",
                table: "DeviceSites",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DeviceSites",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DeviceSites",
                type: "int",
                nullable: false,
                defaultValue: 1);   // التركيبات الموجودة: «يعمل»

            migrationBuilder.AddColumn<int>(
                name: "Vlan",
                table: "DeviceSites",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Devices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Devices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Devices",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Devices",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "DeviceInventoryLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceInventoryLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceInventoryLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_Ip",
                table: "DeviceSites",
                column: "Ip");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_SiteId_Ip",
                table: "DeviceSites",
                columns: new[] { "SiteId", "Ip" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_SN",
                table: "DeviceSites",
                column: "SN");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_Status",
                table: "DeviceSites",
                column: "Status");

            // الكتالوج: قصّ الفراغات ثم دمج الأجهزة المكررة (نفس الاسم والموديل) قبل الفهرس الفريد —
            // تركيبات النسخة المكررة تنتقل إلى الأقدم، ويُضاف وصفها إلى وصفه إن اختلف
            migrationBuilder.Sql(@"
UPDATE Devices SET Name = LTRIM(RTRIM(Name)), Model = LTRIM(RTRIM(Model));

WITH ranked AS (
    SELECT Id, Name, Model, Description, MIN(Id) OVER (PARTITION BY Name, Model) AS KeepId FROM Devices
)
UPDATE ds SET DeviceId = r.KeepId
FROM DeviceSites ds INNER JOIN ranked r ON r.Id = ds.DeviceId
WHERE r.Id <> r.KeepId;

WITH ranked AS (
    SELECT Id, Name, Model, Description, MIN(Id) OVER (PARTITION BY Name, Model) AS KeepId FROM Devices
)
UPDATE keep SET Description = LEFT(keep.Description + N' | ' + dup.Description, 500)
FROM Devices keep INNER JOIN ranked dup ON dup.KeepId = keep.Id AND dup.Id <> keep.Id
WHERE dup.Description <> N'' AND dup.Description <> keep.Description;

WITH ranked AS (
    SELECT Id, MIN(Id) OVER (PARTITION BY Name, Model) AS KeepId FROM Devices
)
DELETE d FROM Devices d INNER JOIN ranked r ON r.Id = d.Id WHERE r.Id <> r.KeepId;");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Name_Model",
                table: "Devices",
                columns: new[] { "Name", "Model" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceInventoryLogs_EntityType_EntityId_CreatedAt",
                table: "DeviceInventoryLogs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceInventoryLogs_UserId_CreatedAt",
                table: "DeviceInventoryLogs",
                columns: new[] { "UserId", "CreatedAt" });

            // صلاحية إظهار كلمات السر (قرار المستخدم 2026-10-05: لمن يملك تعديل الأجهزة)، وأوصاف الصلاحيات الأربع بعد المراجعة
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'RevealDevicePasswords', N'إظهار كلمات سر الأجهزة ونسخها (يُسجَّل كل إظهار)'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'RevealDevicePasswords');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
CROSS JOIN (SELECT Id FROM Permissions WHERE Name = N'RevealDevicePasswords') np
WHERE p.Name = N'EditDevice'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);

UPDATE Permissions SET Description = N'عرض مواقع الأجهزة والكتالوج والتركيبات وسجلها (بلا كلمات السر)' WHERE Name = N'ViewDevices';
UPDATE Permissions SET Description = N'إضافة مواقع وأجهزة وتركيبات واستيرادها من Excel' WHERE Name = N'CreateDevice';
UPDATE Permissions SET Description = N'تعديل المواقع والأجهزة والتركيبات' WHERE Name = N'EditDevice';
UPDATE Permissions SET Description = N'حذف المواقع والأجهزة والتركيبات' WHERE Name = N'DeleteDevice';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ملاحظة: كلمات السر تبقى مشفّرة بعد التراجع (لا يُفك تشفيرها في الترحيل)
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'RevealDevicePasswords';");
            migrationBuilder.DropTable(
                name: "DeviceInventoryLogs");

            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_Ip",
                table: "DeviceSites");

            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_SiteId_Ip",
                table: "DeviceSites");

            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_SN",
                table: "DeviceSites");

            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_Status",
                table: "DeviceSites");

            migrationBuilder.DropIndex(
                name: "IX_Devices_Name_Model",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "ResponsibleParty",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "Firmware",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "InstallDate",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "LastVerifiedAt",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "MacAddress",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "Port",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "Vlan",
                table: "DeviceSites");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Devices");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Sites",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Sites",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "DeviceSites",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "SubnetMask",
                table: "DeviceSites",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15);

            migrationBuilder.AlterColumn<string>(
                name: "Pass",
                table: "DeviceSites",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "DeviceSites",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Ip",
                table: "DeviceSites",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_SiteId",
                table: "DeviceSites",
                column: "SiteId");
        }
    }
}
