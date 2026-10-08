using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Remove_Threshold_Add_Vacation_Request_Signature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReplacementCostThreshold",
                table: "DeviceTypes");

            migrationBuilder.AddColumn<int>(
                name: "RequestSignatureId",
                table: "Vacation",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacation_RequestSignatureId",
                table: "Vacation",
                column: "RequestSignatureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vacation_UserSignatures_RequestSignatureId",
                table: "Vacation",
                column: "RequestSignatureId",
                principalTable: "UserSignatures",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vacation_UserSignatures_RequestSignatureId",
                table: "Vacation");

            migrationBuilder.DropIndex(
                name: "IX_Vacation_RequestSignatureId",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "RequestSignatureId",
                table: "Vacation");

            migrationBuilder.AddColumn<decimal>(
                name: "ReplacementCostThreshold",
                table: "DeviceTypes",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
