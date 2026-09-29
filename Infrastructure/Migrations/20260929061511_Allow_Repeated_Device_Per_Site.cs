using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Allow_Repeated_Device_Per_Site : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_DeviceId_SiteId",
                table: "DeviceSites");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_DeviceId",
                table: "DeviceSites",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceSites_DeviceId",
                table: "DeviceSites");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceSites_DeviceId_SiteId",
                table: "DeviceSites",
                columns: new[] { "DeviceId", "SiteId" },
                unique: true);
        }
    }
}
