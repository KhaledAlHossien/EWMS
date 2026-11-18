using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Response_Usd_Pricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CostsInUsd",
                table: "StudyAssignmentResponses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UsdToSypRate",
                table: "StudyAssignmentResponses",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyAssignmentResponses_UsdToSypRate",
                table: "StudyAssignmentResponses",
                sql: "[UsdToSypRate] IS NULL OR ([CostsInUsd] = 1 AND [UsdToSypRate] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyAssignmentResponses_UsdToSypRate",
                table: "StudyAssignmentResponses");

            migrationBuilder.DropColumn(
                name: "CostsInUsd",
                table: "StudyAssignmentResponses");

            migrationBuilder.DropColumn(
                name: "UsdToSypRate",
                table: "StudyAssignmentResponses");
        }
    }
}
