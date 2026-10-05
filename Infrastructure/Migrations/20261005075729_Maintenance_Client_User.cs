using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Maintenance_Client_User : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientUserId",
                table: "MaintenanceRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ClientUserId_CreatedAt",
                table: "MaintenanceRequests",
                columns: new[] { "ClientUserId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_Users_ClientUserId",
                table: "MaintenanceRequests",
                column: "ClientUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // «أجهزتي في الصيانة» (قرار المستخدم 2026-10-05): أي موظف قد يكون عميلاً، فتُمنح لكل الأدوار الحالية (تُسحب من صفحة الأدوار)
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'ViewMyMaintenanceRequests', N'متابعة طلبات صيانة أجهزته (هو عميلها) وحالتها'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'ViewMyMaintenanceRequests');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN (SELECT Id FROM Permissions WHERE Name = N'ViewMyMaintenanceRequests') p
WHERE NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'ViewMyMaintenanceRequests';");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_Users_ClientUserId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_ClientUserId_CreatedAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "ClientUserId",
                table: "MaintenanceRequests");
        }
    }
}
