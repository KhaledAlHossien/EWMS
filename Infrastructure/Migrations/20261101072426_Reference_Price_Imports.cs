using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Reference_Price_Imports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImportId",
                table: "ReferencePrices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReferencePriceImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    ImportedByUserId = table.Column<int>(type: "int", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferencePriceImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferencePriceImports_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferencePriceImports_Users_ImportedByUserId",
                        column: x => x.ImportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_ImportId",
                table: "ReferencePrices",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePriceImports_BranchId_ImportedAt",
                table: "ReferencePriceImports",
                columns: new[] { "BranchId", "ImportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePriceImports_ImportedByUserId",
                table: "ReferencePriceImports",
                column: "ImportedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReferencePrices_ReferencePriceImports_ImportId",
                table: "ReferencePrices",
                column: "ImportId",
                principalTable: "ReferencePriceImports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReferencePrices_ReferencePriceImports_ImportId",
                table: "ReferencePrices");

            migrationBuilder.DropTable(
                name: "ReferencePriceImports");

            migrationBuilder.DropIndex(
                name: "IX_ReferencePrices_ImportId",
                table: "ReferencePrices");

            migrationBuilder.DropColumn(
                name: "ImportId",
                table: "ReferencePrices");
        }
    }
}
