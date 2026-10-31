using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Pricing_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    CorrespondenceId = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OutsideContractScope = table.Column<bool>(type: "bit", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    AssigneeUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: true),
                    RecommendationNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecommendedByUserId = table.Column<int>(type: "int", nullable: true),
                    RecommendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ResultRecipient = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ResultSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultSentByUserId = table.Column<int>(type: "int", nullable: true),
                    ResultNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverdueNotifiedFor = table.Column<DateTime>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Users_RecommendedByUserId",
                        column: x => x.RecommendedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingRequests_Users_ResultSentByUserId",
                        column: x => x.ResultSentByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AgreementDate = table.Column<DateTime>(type: "date", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    PricingRequestId = table.Column<int>(type: "int", nullable: true),
                    TechnicalSpecs = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ExecutionMethod = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PreparedByUserId = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovalNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReturnNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_Contractors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_PricingRequests_PricingRequestId",
                        column: x => x.PricingRequestId,
                        principalTable: "PricingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingAgreements_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PricingRequestId = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    OfferNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "date", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SupplyTerms = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingOffers_Contractors_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingOffers_PricingRequests_PricingRequestId",
                        column: x => x.PricingRequestId,
                        principalTable: "PricingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingOffers_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingOffers_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PricingRequestId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Specifications = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRequestItems", x => x.Id);
                    table.CheckConstraint("CK_PricingRequestItems_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_PricingRequestItems_PricingRequests_PricingRequestId",
                        column: x => x.PricingRequestId,
                        principalTable: "PricingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingAgreementLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PricingAgreementId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingAgreementLines", x => x.Id);
                    table.CheckConstraint("CK_PricingAgreementLines_Price", "[UnitPrice] >= 0");
                    table.CheckConstraint("CK_PricingAgreementLines_Quantity", "[Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_PricingAgreementLines_PricingAgreements_PricingAgreementId",
                        column: x => x.PricingAgreementId,
                        principalTable: "PricingAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PricingOfferLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PricingOfferId = table.Column<int>(type: "int", nullable: false),
                    PricingRequestItemId = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Conformity = table.Column<int>(type: "int", nullable: false),
                    ConformityNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsSelected = table.Column<bool>(type: "bit", nullable: false),
                    SelectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingOfferLines", x => x.Id);
                    table.CheckConstraint("CK_PricingOfferLines_Price", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_PricingOfferLines_PricingOffers_PricingOfferId",
                        column: x => x.PricingOfferId,
                        principalTable: "PricingOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PricingOfferLines_PricingRequestItems_PricingRequestItemId",
                        column: x => x.PricingRequestItemId,
                        principalTable: "PricingRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreementLines_PricingAgreementId_SortOrder",
                table: "PricingAgreementLines",
                columns: new[] { "PricingAgreementId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_ApprovedByUserId",
                table: "PricingAgreements",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_BranchId_Status",
                table: "PricingAgreements",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_BranchNumber",
                table: "PricingAgreements",
                columns: new[] { "BranchId", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_ContractId",
                table: "PricingAgreements",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_DocumentVersionId",
                table: "PricingAgreements",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_PreparedByUserId",
                table: "PricingAgreements",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_PricingRequestId",
                table: "PricingAgreements",
                column: "PricingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_ProjectId",
                table: "PricingAgreements",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAgreements_SupplierId",
                table: "PricingAgreements",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingOfferLines_OneSelected",
                table: "PricingOfferLines",
                column: "PricingRequestItemId",
                unique: true,
                filter: "[IsSelected] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PricingOfferLines_PricingOfferId_PricingRequestItemId",
                table: "PricingOfferLines",
                columns: new[] { "PricingOfferId", "PricingRequestItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingOffers_DocumentVersionId",
                table: "PricingOffers",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingOffers_PricingRequestId_SupplierId",
                table: "PricingOffers",
                columns: new[] { "PricingRequestId", "SupplierId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingOffers_RecordedByUserId",
                table: "PricingOffers",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingOffers_SupplierId",
                table: "PricingOffers",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequestItems_PricingRequestId_SortOrder",
                table: "PricingRequestItems",
                columns: new[] { "PricingRequestId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_AssignedByUserId",
                table: "PricingRequests",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_AssigneeUserId",
                table: "PricingRequests",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_BranchId_Status",
                table: "PricingRequests",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_ContractId",
                table: "PricingRequests",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_CorrespondenceId",
                table: "PricingRequests",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_DecidedByUserId",
                table: "PricingRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_ProjectId",
                table: "PricingRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_RecommendedByUserId",
                table: "PricingRequests",
                column: "RecommendedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_RegisteredByUserId",
                table: "PricingRequests",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingRequests_ResultSentByUserId",
                table: "PricingRequests",
                column: "ResultSentByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingAgreementLines");

            migrationBuilder.DropTable(
                name: "PricingOfferLines");

            migrationBuilder.DropTable(
                name: "PricingAgreements");

            migrationBuilder.DropTable(
                name: "PricingOffers");

            migrationBuilder.DropTable(
                name: "PricingRequestItems");

            migrationBuilder.DropTable(
                name: "PricingRequests");
        }
    }
}
