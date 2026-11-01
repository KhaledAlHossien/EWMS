using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Reference_Prices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferencePrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Specifications = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PriceDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceDocument = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SupersedesPriceId = table.Column<int>(type: "int", nullable: true),
                    CorrectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferencePrices", x => x.Id);
                    table.CheckConstraint("CK_ReferencePrices_Price", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_ReferencePrices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferencePrices_Contractors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferencePrices_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferencePrices_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferencePrices_ReferencePrices_SupersedesPriceId",
                        column: x => x.SupersedesPriceId,
                        principalTable: "ReferencePrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferencePrices_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReferencePrices_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_ApprovedByUserId",
                table: "ReferencePrices",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_BranchId_ItemCode",
                table: "ReferencePrices",
                columns: new[] { "BranchId", "ItemCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_BranchId_ItemName",
                table: "ReferencePrices",
                columns: new[] { "BranchId", "ItemName" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_BranchId_Status",
                table: "ReferencePrices",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_CreatedByUserId",
                table: "ReferencePrices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_DocumentVersionId",
                table: "ReferencePrices",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_OneCorrection",
                table: "ReferencePrices",
                column: "SupersedesPriceId",
                unique: true,
                filter: "[SupersedesPriceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_PriceDate",
                table: "ReferencePrices",
                column: "PriceDate");

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_ProjectId",
                table: "ReferencePrices",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferencePrices_SupplierId",
                table: "ReferencePrices",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferencePrices");
        }
    }
}
