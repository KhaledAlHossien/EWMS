using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Maintenance_Delivery_Signature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDelivery",
                table: "MaintenanceRequestStatuses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "MaintenanceRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliverySignatureId",
                table: "MaintenanceRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliverySignerId",
                table: "MaintenanceRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_DeliverySignatureId",
                table: "MaintenanceRequests",
                column: "DeliverySignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_DeliverySignerId",
                table: "MaintenanceRequests",
                column: "DeliverySignerId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_UserSignatures_DeliverySignatureId",
                table: "MaintenanceRequests",
                column: "DeliverySignatureId",
                principalTable: "UserSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_Users_DeliverySignerId",
                table: "MaintenanceRequests",
                column: "DeliverySignerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // الحالة الافتراضية "تم التسليم" هي حالة التسليم (تُعدَّل من إعدادات الصيانة)
            migrationBuilder.Sql("UPDATE MaintenanceRequestStatuses SET IsDelivery = 1 WHERE Name = N'تم التسليم';");

            // الطلبات المسلَّمة قبل هذا التعديل: يُثبَّت لها ما كانت تطبعه ورقة التسليم حتى الآن —
            // صاحب SignMaintenanceReceipt الأقدم في قسم الطلب وتوقيعه الحالي، وتاريخ آخر تحديث كتاريخ تسليم تقريبي
            migrationBuilder.Sql(@"
UPDATE r SET
    DeliveredAt = r.UpdatedAt,
    DeliverySignerId = signer.UserId,
    DeliverySignatureId = sig.Id
FROM MaintenanceRequests r
INNER JOIN MaintenanceRequestStatuses st ON st.Id = r.MaintenanceRequestStatusId AND st.IsDelivery = 1
OUTER APPLY (
    SELECT TOP 1 u.Id AS UserId
    FROM Users u
    INNER JOIN RolePermissions rp ON rp.RoleId = u.RoleId
    INNER JOIN Permissions p ON p.Id = rp.PermissionId AND p.Name = N'SignMaintenanceReceipt'
    WHERE u.IsActive = 1 AND u.DepartmentId = r.DepartmentId
    ORDER BY u.Id) signer
OUTER APPLY (
    SELECT TOP 1 us.Id FROM UserSignatures us WHERE us.UserId = signer.UserId AND us.IsCurrent = 1) sig
WHERE r.DeliveredAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_UserSignatures_DeliverySignatureId",
                table: "MaintenanceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_Users_DeliverySignerId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_DeliverySignatureId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_DeliverySignerId",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "IsDelivery",
                table: "MaintenanceRequestStatuses");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "DeliverySignatureId",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "DeliverySignerId",
                table: "MaintenanceRequests");
        }
    }
}
