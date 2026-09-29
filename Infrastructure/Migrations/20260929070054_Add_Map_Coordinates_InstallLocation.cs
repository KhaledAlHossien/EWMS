using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Map_Coordinates_InstallLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Sites",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Sites",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Regions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Regions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstallLocation",
                table: "DeviceSites",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            // نقل بيانات "الموقع الجغرافي" النصي قبل حذفه (بدون خسارة بيانات):
            // 1) النص بصيغة "خط العرض، خط الطول" → Latitude/Longitude (الفاصلة العربية أو الإنجليزية)
            migrationBuilder.Sql(@"
UPDATE Sites SET
    Latitude  = TRY_CAST(LTRIM(RTRIM(LEFT(REPLACE(Location, N'،', N','), CHARINDEX(N',', REPLACE(Location, N'،', N',')) - 1))) AS float),
    Longitude = TRY_CAST(LTRIM(RTRIM(SUBSTRING(REPLACE(Location, N'،', N','), CHARINDEX(N',', REPLACE(Location, N'،', N',')) + 1, 200))) AS float)
WHERE CHARINDEX(N',', REPLACE(Location, N'،', N',')) > 0;

-- إحداثيات ناقصة أو خارج سوريا تُعتبر غير صالحة
UPDATE Sites SET Latitude = NULL, Longitude = NULL
WHERE Latitude IS NULL OR Longitude IS NULL
   OR Latitude NOT BETWEEN 32.0 AND 37.6 OR Longitude NOT BETWEEN 35.5 AND 42.6;");

            // 2) أي نص لم يُحوَّل لإحداثيات يُحفظ في وصف الموقع
            migrationBuilder.Sql(@"
UPDATE Sites SET Description = LEFT(
        CASE WHEN LTRIM(RTRIM(Description)) = N'' THEN N'' ELSE Description + N' — ' END + N'الموقع السابق: ' + Location, 500)
WHERE Latitude IS NULL AND LTRIM(RTRIM(ISNULL(Location, N''))) <> N'';");

            // 3) الآن فقط نحذف العمود القديم
            migrationBuilder.DropColumn(
                name: "Location",
                table: "Sites");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "InstallLocation",
                table: "DeviceSites");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Sites",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
