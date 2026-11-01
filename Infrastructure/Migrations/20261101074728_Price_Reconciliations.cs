using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Price_Reconciliations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceReconciliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    DocumentKind = table.Column<int>(type: "int", nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "date", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AuditResult = table.Column<int>(type: "int", nullable: true),
                    AuditNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PreparedByUserId = table.Column<int>(type: "int", nullable: false),
                    AuditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferredTo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferralNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReferredByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_Contractors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliations_Users_ReferredByUserId",
                        column: x => x.ReferredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceReconciliationLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PriceReconciliationId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    InvoiceUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ReferenceKind = table.Column<int>(type: "int", nullable: false),
                    ReferenceOfferLineId = table.Column<int>(type: "int", nullable: true),
                    ReferencePriceId = table.Column<int>(type: "int", nullable: true),
                    ReferenceLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ReferenceQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ReferenceUnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ReferenceUnit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ReferenceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceReconciliationLines", x => x.Id);
                    table.CheckConstraint("CK_PriceReconciliationLines_Price", "[InvoiceUnitPrice] >= 0");
                    table.CheckConstraint("CK_PriceReconciliationLines_Quantity", "[InvoiceQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_PriceReconciliationLines_PriceReconciliations_PriceReconciliationId",
                        column: x => x.PriceReconciliationId,
                        principalTable: "PriceReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliationLines_PricingOfferLines_ReferenceOfferLineId",
                        column: x => x.ReferenceOfferLineId,
                        principalTable: "PricingOfferLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceReconciliationLines_ReferencePrices_ReferencePriceId",
                        column: x => x.ReferencePriceId,
                        principalTable: "ReferencePrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliationLines_PriceReconciliationId_SortOrder",
                table: "PriceReconciliationLines",
                columns: new[] { "PriceReconciliationId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliationLines_ReferenceOfferLineId",
                table: "PriceReconciliationLines",
                column: "ReferenceOfferLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliationLines_ReferencePriceId",
                table: "PriceReconciliationLines",
                column: "ReferencePriceId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_BranchId_Status",
                table: "PriceReconciliations",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_ContractId",
                table: "PriceReconciliations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_DecidedByUserId",
                table: "PriceReconciliations",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_Document",
                table: "PriceReconciliations",
                columns: new[] { "BranchId", "SupplierId", "DocumentKind", "DocumentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_DocumentVersionId",
                table: "PriceReconciliations",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_PreparedByUserId",
                table: "PriceReconciliations",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_ProjectId",
                table: "PriceReconciliations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_ReferredByUserId",
                table: "PriceReconciliations",
                column: "ReferredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceReconciliations_SupplierId",
                table: "PriceReconciliations",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceReconciliationLines");

            migrationBuilder.DropTable(
                name: "PriceReconciliations");
        }
    }
}
