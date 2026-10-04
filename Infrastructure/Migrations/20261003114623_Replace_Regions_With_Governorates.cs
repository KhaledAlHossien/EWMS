using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Replace_Regions_With_Governorates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // المناطق صارت محافظات ثابتة تُشتق من إحداثيات الموقع (قرار المستخدم 2026-10-03).
            // قبل حذف جدول المناطق نحتفظ باسم منطقة كل موقع في وصفه حتى لا تضيع المعلومة،
            // ثم تُملأ محافظة المواقع القديمة عند أول تشغيل (DbSeeder.BackfillSiteGovernoratesAsync) من إحداثياتها.
            migrationBuilder.Sql(@"
UPDATE s
SET s.Description = CASE WHEN LTRIM(RTRIM(s.Description)) = N'' THEN N'المنطقة السابقة: ' + r.Name
                         ELSE s.Description + N' — المنطقة السابقة: ' + r.Name END
FROM Sites s
INNER JOIN Regions r ON r.Id = s.RegionId;");

            migrationBuilder.DropForeignKey(
                name: "FK_Sites_Regions_RegionId",
                table: "Sites");

            migrationBuilder.DropTable(
                name: "Regions");

            migrationBuilder.DropIndex(
                name: "IX_Sites_RegionId",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Sites");

            migrationBuilder.AddColumn<string>(
                name: "GovernorateCode",
                table: "Sites",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_GovernorateCode",
                table: "Sites",
                column: "GovernorateCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sites_GovernorateCode",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "GovernorateCode",
                table: "Sites");

            migrationBuilder.AddColumn<int>(
                name: "RegionId",
                table: "Sites",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Regions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sites_RegionId",
                table: "Sites",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Regions_Name",
                table: "Regions",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_Regions_RegionId",
                table: "Sites",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
