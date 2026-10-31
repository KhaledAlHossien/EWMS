using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Contracts_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contractors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contractors_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCriteria", x => x.Id);
                    table.CheckConstraint("CK_EvaluationCriteria_Weight", "[Weight] IS NULL OR [Weight] > 0");
                    table.ForeignKey(
                        name: "FK_EvaluationCriteria_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CorrespondenceId = table.Column<int>(type: "int", nullable: true),
                    ExecutionOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExecutionOrderDate = table.Column<DateTime>(type: "date", nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ContractorId = table.Column<int>(type: "int", nullable: true),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContractDate = table.Column<DateTime>(type: "date", nullable: true),
                    StartDate = table.Column<DateTime>(type: "date", nullable: true),
                    DurationDays = table.Column<int>(type: "int", nullable: true),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TenderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TenderSentAt = table.Column<DateTime>(type: "date", nullable: true),
                    TenderOpenedAt = table.Column<DateTime>(type: "date", nullable: true),
                    AwardedAt = table.Column<DateTime>(type: "date", nullable: true),
                    AwardNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: true),
                    WarrantyStart = table.Column<DateTime>(type: "date", nullable: true),
                    WarrantyEnd = table.Column<DateTime>(type: "date", nullable: true),
                    WarrantyNotifiedFor = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.CheckConstraint("CK_Contracts_Duration", "[DurationDays] IS NULL OR [DurationDays] > 0");
                    table.CheckConstraint("CK_Contracts_Value", "[Value] IS NULL OR [Value] >= 0");
                    table.CheckConstraint("CK_Contracts_Warranty", "[WarrantyEnd] IS NULL OR [WarrantyStart] IS NULL OR [WarrantyEnd] >= [WarrantyStart]");
                    table.ForeignKey(
                        name: "FK_Contracts_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractAmendments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValueChange = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DurationChangeDays = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "date", nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractAmendments_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    ContractorId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsAwarded = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractOffers", x => x.Id);
                    table.CheckConstraint("CK_ContractOffers_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_ContractOffers_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractOffers_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractOffers_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractorEvaluations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    ContractorId = table.Column<int>(type: "int", nullable: false),
                    EvaluatedByUserId = table.Column<int>(type: "int", nullable: false),
                    EvaluationDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorEvaluations_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorEvaluations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorEvaluations_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorEvaluations_Users_EvaluatedByUserId",
                        column: x => x.EvaluatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractorEvaluations_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandoverRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "date", nullable: false),
                    CorrespondenceId = table.Column<int>(type: "int", nullable: true),
                    RequestNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "date", nullable: true),
                    Result = table.Column<int>(type: "int", nullable: true),
                    ResultNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MinutesVersionId = table.Column<int>(type: "int", nullable: true),
                    SupervisionReportVersionId = table.Column<int>(type: "int", nullable: true),
                    ConcludedByUserId = table.Column<int>(type: "int", nullable: true),
                    ConcludedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_ProjectDocumentVersions_MinutesVersionId",
                        column: x => x.MinutesVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_ProjectDocumentVersions_SupervisionReportVersionId",
                        column: x => x.SupervisionReportVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Users_ConcludedByUserId",
                        column: x => x.ConcludedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverRecords_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WithheldAt = table.Column<DateTime>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionEntries", x => x.Id);
                    table.CheckConstraint("CK_RetentionEntries_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_RetentionEntries_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetentionEntries_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationId = table.Column<int>(type: "int", nullable: false),
                    CriterionId = table.Column<int>(type: "int", nullable: false),
                    CriterionName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationScores", x => x.Id);
                    table.CheckConstraint("CK_EvaluationScores_Score", "[Score] IS NULL OR ([Score] >= 0 AND [Score] <= 100)");
                    table.ForeignKey(
                        name: "FK_EvaluationScores_ContractorEvaluations_EvaluationId",
                        column: x => x.EvaluationId,
                        principalTable: "ContractorEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationScores_EvaluationCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "EvaluationCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Committees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: true),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: true),
                    FormedAt = table.Column<DateTime>(type: "date", nullable: false),
                    DecisionNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DecisionVersionId = table.Column<int>(type: "int", nullable: true),
                    Recommendations = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Committees", x => x.Id);
                    table.CheckConstraint("CK_Committees_Owner", "([Kind] = 1 AND [ContractId] IS NOT NULL AND [HandoverRecordId] IS NULL) OR ([Kind] = 2 AND [HandoverRecordId] IS NOT NULL AND [ContractId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_Committees_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Committees_HandoverRecords_HandoverRecordId",
                        column: x => x.HandoverRecordId,
                        principalTable: "HandoverRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Committees_ProjectDocumentVersions_DecisionVersionId",
                        column: x => x.DecisionVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Committees_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Committees_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HandoverNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ResponsibleUserId = table.Column<int>(type: "int", nullable: true),
                    ResponsibleName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DueDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FixedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FixedByUserId = table.Column<int>(type: "int", nullable: true),
                    FixNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedByUserId = table.Column<int>(type: "int", nullable: true),
                    EvidenceVersionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoverNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_HandoverRecords_HandoverRecordId",
                        column: x => x.HandoverRecordId,
                        principalTable: "HandoverRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_ProjectDocumentVersions_EvidenceVersionId",
                        column: x => x.EvidenceVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_Users_FixedByUserId",
                        column: x => x.FixedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_Users_ResponsibleUserId",
                        column: x => x.ResponsibleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HandoverNotes_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RetentionEntryId = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "date", nullable: false),
                    ApprovingAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionDecisions_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetentionDecisions_RetentionEntries_RetentionEntryId",
                        column: x => x.RetentionEntryId,
                        principalTable: "RetentionEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RetentionDecisions_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommitteeMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CommitteeId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    ExternalName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Capacity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsChair = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMembers", x => x.Id);
                    table.CheckConstraint("CK_CommitteeMembers_Who", "([UserId] IS NOT NULL AND [ExternalName] IS NULL) OR ([UserId] IS NULL AND [ExternalName] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_Committees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_OneChair",
                table: "CommitteeMembers",
                column: "CommitteeId",
                unique: true,
                filter: "[IsChair] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_OnePerUser",
                table: "CommitteeMembers",
                columns: new[] { "CommitteeId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_UserId",
                table: "CommitteeMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_ContractId",
                table: "Committees",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_CreatedByUserId",
                table: "Committees",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_DecisionVersionId",
                table: "Committees",
                column: "DecisionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_OnePerHandover",
                table: "Committees",
                column: "HandoverRecordId",
                unique: true,
                filter: "[HandoverRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_ProjectId",
                table: "Committees",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_ContractId_Number",
                table: "ContractAmendments",
                columns: new[] { "ContractId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_DocumentVersionId",
                table: "ContractAmendments",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractAmendments_RecordedByUserId",
                table: "ContractAmendments",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractOffers_ContractId_ContractorId",
                table: "ContractOffers",
                columns: new[] { "ContractId", "ContractorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractOffers_ContractorId",
                table: "ContractOffers",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractOffers_OneAwarded",
                table: "ContractOffers",
                column: "ContractId",
                unique: true,
                filter: "[IsAwarded] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ContractOffers_RecordedByUserId",
                table: "ContractOffers",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorEvaluations_ContractId_Status",
                table: "ContractorEvaluations",
                columns: new[] { "ContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorEvaluations_ContractorId",
                table: "ContractorEvaluations",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorEvaluations_DocumentVersionId",
                table: "ContractorEvaluations",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorEvaluations_EvaluatedByUserId",
                table: "ContractorEvaluations",
                column: "EvaluatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorEvaluations_ReviewedByUserId",
                table: "ContractorEvaluations",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contractors_BranchId_Name",
                table: "Contractors",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_BranchNumber",
                table: "Contracts",
                columns: new[] { "BranchId", "Number" },
                unique: true,
                filter: "[Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ContractorId",
                table: "Contracts",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CorrespondenceId",
                table: "Contracts",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CreatedByUserId",
                table: "Contracts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProjectId_Status",
                table: "Contracts",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_WarrantyEnd",
                table: "Contracts",
                column: "WarrantyEnd");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCriteria_BranchId_Name",
                table: "EvaluationCriteria",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScores_CriterionId",
                table: "EvaluationScores",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationScores_EvaluationId_CriterionId",
                table: "EvaluationScores",
                columns: new[] { "EvaluationId", "CriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_EvidenceVersionId",
                table: "HandoverNotes",
                column: "EvidenceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_FixedByUserId",
                table: "HandoverNotes",
                column: "FixedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_HandoverRecordId_Status",
                table: "HandoverNotes",
                columns: new[] { "HandoverRecordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_ResponsibleUserId",
                table: "HandoverNotes",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverNotes_VerifiedByUserId",
                table: "HandoverNotes",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_ConcludedByUserId",
                table: "HandoverRecords",
                column: "ConcludedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_ContractId",
                table: "HandoverRecords",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_CorrespondenceId",
                table: "HandoverRecords",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_MinutesVersionId",
                table: "HandoverRecords",
                column: "MinutesVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_OneFinalConcluded",
                table: "HandoverRecords",
                column: "ProjectId",
                unique: true,
                filter: "[Kind] = 2 AND [Status] = 3 AND [Result] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_OneOpenPerKind",
                table: "HandoverRecords",
                columns: new[] { "ProjectId", "Kind" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_ProjectId_Kind_Status",
                table: "HandoverRecords",
                columns: new[] { "ProjectId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_RequestedByUserId",
                table: "HandoverRecords",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoverRecords_SupervisionReportVersionId",
                table: "HandoverRecords",
                column: "SupervisionReportVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionDecisions_DocumentVersionId",
                table: "RetentionDecisions",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionDecisions_RecordedByUserId",
                table: "RetentionDecisions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionDecisions_RetentionEntryId",
                table: "RetentionDecisions",
                column: "RetentionEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionEntries_ContractId",
                table: "RetentionEntries",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionEntries_RecordedByUserId",
                table: "RetentionEntries",
                column: "RecordedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommitteeMembers");

            migrationBuilder.DropTable(
                name: "ContractAmendments");

            migrationBuilder.DropTable(
                name: "ContractOffers");

            migrationBuilder.DropTable(
                name: "EvaluationScores");

            migrationBuilder.DropTable(
                name: "HandoverNotes");

            migrationBuilder.DropTable(
                name: "RetentionDecisions");

            migrationBuilder.DropTable(
                name: "Committees");

            migrationBuilder.DropTable(
                name: "ContractorEvaluations");

            migrationBuilder.DropTable(
                name: "EvaluationCriteria");

            migrationBuilder.DropTable(
                name: "RetentionEntries");

            migrationBuilder.DropTable(
                name: "HandoverRecords");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Contractors");
        }
    }
}
