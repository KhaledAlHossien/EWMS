using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Move_SN_To_Installation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // الجهاز صار نوعاً قابلاً للتكرار، والرقم التسلسلي يخص كل قطعة مركّبة (قرار المستخدم 2026-09-29)
            // 1) عمود جديد في التركيبات
            migrationBuilder.AddColumn<string>(
                name: "SN",
                table: "DeviceSites",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // 2) كل تركيب يأخذ الرقم التسلسلي الذي كان مسجّلاً على جهازه
            migrationBuilder.Sql(@"
UPDATE ds SET ds.SN = d.SN
FROM DeviceSites ds INNER JOIN Devices d ON d.Id = ds.DeviceId
WHERE LTRIM(RTRIM(ISNULL(d.SN, N''))) <> N'';");

            // 3) جهاز له رقم تسلسلي بلا أي تركيب: نحفظ الرقم في وصفه حتى لا يضيع
            migrationBuilder.Sql(@"
UPDATE d SET d.Description = LEFT(
        CASE WHEN LTRIM(RTRIM(d.Description)) = N'' THEN N'' ELSE d.Description + N' — ' END + N'الرقم التسلسلي السابق: ' + d.SN, 500)
FROM Devices d
WHERE LTRIM(RTRIM(ISNULL(d.SN, N''))) <> N''
  AND NOT EXISTS (SELECT 1 FROM DeviceSites ds WHERE ds.DeviceId = d.Id);");

            // 4) الآن فقط نحذف العمود من الأجهزة
            migrationBuilder.DropColumn(
                name: "SN",
                table: "Devices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SN",
                table: "DeviceSites");

            migrationBuilder.AddColumn<string>(
                name: "SN",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
