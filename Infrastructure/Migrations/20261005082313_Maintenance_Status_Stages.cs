using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Maintenance_Status_Stages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // مرحلة ثابتة لكل حالة (قرار المستخدم 2026-10-05) تحلّ محل خانة «حالة تسليم»:
            // التسليم → مُسلَّم، والحالات الافتراضية بأسمائها، وغيرها «قيد العمل» مؤقتاً (يُعدَّل من إعدادات الصيانة)
            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "MaintenanceRequestStatuses",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql(@"
UPDATE MaintenanceRequestStatuses SET Stage = CASE
    WHEN IsDelivery = 1 THEN 4
    WHEN Name = N'جديد' THEN 1
    WHEN Name = N'قيد الصيانة' THEN 2
    WHEN Name = N'تم الإصلاح' THEN 3
    WHEN Name LIKE N'%غير قابل%' THEN 5
    ELSE 2 END;");

            migrationBuilder.DropColumn(
                name: "IsDelivery",
                table: "MaintenanceRequestStatuses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDelivery",
                table: "MaintenanceRequestStatuses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE MaintenanceRequestStatuses SET IsDelivery = CASE WHEN Stage = 4 THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "MaintenanceRequestStatuses");
        }
    }
}
