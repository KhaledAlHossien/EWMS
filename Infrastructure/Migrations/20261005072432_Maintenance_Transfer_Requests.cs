using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Maintenance_Transfer_Requests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceTransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: false),
                    RequestedById = table.Column<int>(type: "int", nullable: false),
                    SuggestedUserId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedById = table.Column<int>(type: "int", nullable: true),
                    NewUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTransferRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceTransferRequests_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceTransferRequests_Users_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTransferRequests_Users_NewUserId",
                        column: x => x.NewUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTransferRequests_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTransferRequests_Users_SuggestedUserId",
                        column: x => x.SuggestedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTransferRequests_DecidedById",
                table: "MaintenanceTransferRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTransferRequests_MaintenanceRequestId",
                table: "MaintenanceTransferRequests",
                column: "MaintenanceRequestId",
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTransferRequests_NewUserId",
                table: "MaintenanceTransferRequests",
                column: "NewUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTransferRequests_RequestedById",
                table: "MaintenanceTransferRequests",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTransferRequests_Status_CreatedAt",
                table: "MaintenanceTransferRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTransferRequests_SuggestedUserId",
                table: "MaintenanceTransferRequests",
                column: "SuggestedUserId");

            // صلاحية طلب التحويل (قرار المستخدم 2026-10-05): تُمنح تلقائياً لكل دور يملك إنشاء طلب صيانة
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'RequestMaintenanceTransfer', N'طلب تحويل طلب صيانة مسند إليه إلى موظف آخر (يقرّره رئيس القسم)'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'RequestMaintenanceTransfer');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT rp.RoleId, np.Id
FROM RolePermissions rp
INNER JOIN Permissions p ON p.Id = rp.PermissionId
CROSS JOIN (SELECT Id FROM Permissions WHERE Name = N'RequestMaintenanceTransfer') np
WHERE p.Name = N'CreateMaintenanceRequest'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = rp.RoleId AND x.PermissionId = np.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'RequestMaintenanceTransfer';");

            migrationBuilder.DropTable(
                name: "MaintenanceTransferRequests");
        }
    }
}
