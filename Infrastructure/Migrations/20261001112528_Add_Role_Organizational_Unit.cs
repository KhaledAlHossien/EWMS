using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Role_Organizational_Unit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OfficeId",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_BranchId",
                table: "Roles",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_DepartmentId",
                table: "Roles",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_OfficeId",
                table: "Roles",
                column: "OfficeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Branches_BranchId",
                table: "Roles",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Departments_DepartmentId",
                table: "Roles",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Offices_OfficeId",
                table: "Roles",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Branches_BranchId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Departments_DepartmentId",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Offices_OfficeId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_BranchId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_DepartmentId",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Roles_OfficeId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "OfficeId",
                table: "Roles");
        }
    }
}
