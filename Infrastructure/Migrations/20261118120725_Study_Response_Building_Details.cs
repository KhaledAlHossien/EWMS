using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Response_Building_Details : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResponseBuildingDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    BuildingType = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Function = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Area = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseBuildingDetails", x => x.Id);
                    table.CheckConstraint("CK_ResponseBuildingDetails_Area", "[Area] > 0");
                    table.ForeignKey(
                        name: "FK_ResponseBuildingDetails_ResponseBuildings_ResponseId_BuildingType",
                        columns: x => new { x.ResponseId, x.BuildingType },
                        principalTable: "ResponseBuildings",
                        principalColumns: new[] { "ResponseId", "BuildingType" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResponseBuildingDetails_ResponseId_BuildingType",
                table: "ResponseBuildingDetails",
                columns: new[] { "ResponseId", "BuildingType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResponseBuildingDetails");
        }
    }
}
