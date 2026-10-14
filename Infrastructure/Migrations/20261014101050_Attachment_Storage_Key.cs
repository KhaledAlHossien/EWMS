using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Attachment_Storage_Key : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "VacationAttachments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "MapEntryFiles",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "AssignedTaskAttachments",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VacationAttachments_StorageKey",
                table: "VacationAttachments",
                column: "StorageKey",
                unique: true,
                filter: "[StorageKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MapEntryFiles_StorageKey",
                table: "MapEntryFiles",
                column: "StorageKey",
                unique: true,
                filter: "[StorageKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskAttachments_StorageKey",
                table: "AssignedTaskAttachments",
                column: "StorageKey",
                unique: true,
                filter: "[StorageKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VacationAttachments_StorageKey",
                table: "VacationAttachments");

            migrationBuilder.DropIndex(
                name: "IX_MapEntryFiles_StorageKey",
                table: "MapEntryFiles");

            migrationBuilder.DropIndex(
                name: "IX_AssignedTaskAttachments_StorageKey",
                table: "AssignedTaskAttachments");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "VacationAttachments");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "MapEntryFiles");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "AssignedTaskAttachments");
        }
    }
}
