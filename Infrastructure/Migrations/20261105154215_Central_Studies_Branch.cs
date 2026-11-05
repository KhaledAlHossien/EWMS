using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Central_Studies_Branch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EngineeringDisciplines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineeringDisciplines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyAttachmentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyAttachmentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudyNumberSequences",
                columns: table => new
                {
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LastNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyNumberSequences", x => new { x.DocumentType, x.Year });
                });

            migrationBuilder.CreateTable(
                name: "StudyRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestDate = table.Column<DateTime>(type: "date", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    MinistryArchiveNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MinistryArchiveDate = table.Column<DateTime>(type: "date", nullable: true),
                    AdminArchiveNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdminArchiveDate = table.Column<DateTime>(type: "date", nullable: true),
                    BeneficiaryArchiveNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AddedDate = table.Column<DateTime>(type: "date", nullable: true),
                    EntityCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    MainBeneficiary = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubBeneficiary = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GovernorateCode = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CityOrVillage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PropertyNature = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    FollowerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FollowerPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyRequests_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyRequests_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyStageTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultOrder = table.Column<int>(type: "int", nullable: false),
                    DefaultWeight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    LinkedDocument = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyStageTypes", x => x.Id);
                    table.CheckConstraint("CK_StudyStageTypes_Weight", "[DefaultWeight] >= 0 AND [DefaultWeight] <= 100");
                });

            migrationBuilder.CreateTable(
                name: "StudyActivityLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyActivityLogs_StudyRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "StudyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyActivityLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    FollowingDepartmentId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    AssignmentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AssignmentDate = table.Column<DateTime>(type: "date", nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AllocatedBudget = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ScopeSummary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    NeedsTechnicalEvaluation = table.Column<bool>(type: "bit", nullable: false),
                    NeedsResourceEstimate = table.Column<bool>(type: "bit", nullable: false),
                    NeedsPreliminaryBudget = table.Column<bool>(type: "bit", nullable: false),
                    NeedsFinalRecommendation = table.Column<bool>(type: "bit", nullable: false),
                    ReferralDate = table.Column<DateTime>(type: "date", nullable: false),
                    ResponseDeadline = table.Column<DateTime>(type: "date", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyAssignments_Departments_FollowingDepartmentId",
                        column: x => x.FollowingDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyAssignments_StudyRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "StudyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyAssignments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyAssignmentResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: false),
                    ProposedManagerUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ResponseDate = table.Column<DateTime>(type: "date", nullable: false),
                    Recommendation = table.Column<int>(type: "int", nullable: false),
                    RequiredDays = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyAssignmentResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyAssignmentResponses_StudyAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "StudyAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyAssignmentResponses_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyAssignmentResponses_Users_ProposedManagerUserId",
                        column: x => x.ProposedManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResponseBuildings",
                columns: table => new
                {
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    BuildingType = table.Column<int>(type: "int", nullable: false),
                    BuildingCount = table.Column<int>(type: "int", nullable: false),
                    BuildingsArea = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    SiteArea = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: true),
                    InitialCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseBuildings", x => new { x.ResponseId, x.BuildingType });
                    table.CheckConstraint("CK_ResponseBuildings_Count", "[BuildingCount] >= 0");
                    table.ForeignKey(
                        name: "FK_ResponseBuildings_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResponseCostItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseCostItems", x => x.Id);
                    table.CheckConstraint("CK_ResponseCostItems_Values", "[Quantity] >= 0 AND [UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_ResponseCostItems_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResponseLogisticItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    Item = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseLogisticItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResponseLogisticItems_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResponseStaffEstimates",
                columns: table => new
                {
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    DisciplineId = table.Column<int>(type: "int", nullable: false),
                    StaffCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseStaffEstimates", x => new { x.ResponseId, x.DisciplineId });
                    table.CheckConstraint("CK_ResponseStaffEstimates_Count", "[StaffCount] >= 0");
                    table.ForeignKey(
                        name: "FK_ResponseStaffEstimates_EngineeringDisciplines_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "EngineeringDisciplines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResponseStaffEstimates_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyStartOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    CompetentDepartmentId = table.Column<int>(type: "int", nullable: false),
                    ProjectManagerUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "date", nullable: false),
                    StudyStartDate = table.Column<DateTime>(type: "date", nullable: false),
                    StudyDeadline = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyStartOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyStartOrders_Departments_CompetentDepartmentId",
                        column: x => x.CompetentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyStartOrders_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyStartOrders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyStartOrders_Users_ProjectManagerUserId",
                        column: x => x.ProjectManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StartOrderEngineers",
                columns: table => new
                {
                    StartOrderId = table.Column<int>(type: "int", nullable: false),
                    EngineerUserId = table.Column<int>(type: "int", nullable: false),
                    DisciplineId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StartOrderEngineers", x => new { x.StartOrderId, x.EngineerUserId });
                    table.ForeignKey(
                        name: "FK_StartOrderEngineers_EngineeringDisciplines_DisciplineId",
                        column: x => x.DisciplineId,
                        principalTable: "EngineeringDisciplines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StartOrderEngineers_StudyStartOrders_StartOrderId",
                        column: x => x.StartOrderId,
                        principalTable: "StudyStartOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StartOrderEngineers_Users_EngineerUserId",
                        column: x => x.EngineerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StartOrderResources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartOrderId = table.Column<int>(type: "int", nullable: false),
                    Item = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StartOrderResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StartOrderResources_StudyStartOrders_StartOrderId",
                        column: x => x.StartOrderId,
                        principalTable: "StudyStartOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudyProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartOrderId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    ManagerUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlannedStartDate = table.Column<DateTime>(type: "date", nullable: false),
                    PlannedEndDate = table.Column<DateTime>(type: "date", nullable: false),
                    ActualStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyProjects_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyProjects_StudyStartOrders_StartOrderId",
                        column: x => x.StartOrderId,
                        principalTable: "StudyStartOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyProjects_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyProjects_Users_ManagerUserId",
                        column: x => x.ManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    AddedByUserId = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDepartments_StudyProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "StudyProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectDepartments_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    StageTypeId = table.Column<int>(type: "int", nullable: false),
                    ResponsibleDepartmentId = table.Column<int>(type: "int", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PlannedStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStages", x => x.Id);
                    table.CheckConstraint("CK_ProjectStages_Weight", "[Weight] >= 0 AND [Weight] <= 100");
                    table.ForeignKey(
                        name: "FK_ProjectStages_Departments_ResponsibleDepartmentId",
                        column: x => x.ResponsibleDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectStages_StudyProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "StudyProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectStages_StudyStageTypes_StageTypeId",
                        column: x => x.StageTypeId,
                        principalTable: "StudyStageTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectStages_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    ProjectDepartmentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => new { x.ProjectDepartmentId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProjectMembers_ProjectDepartments_ProjectDepartmentId",
                        column: x => x.ProjectDepartmentId,
                        principalTable: "ProjectDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StageTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StageId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    OfficeId = table.Column<int>(type: "int", nullable: true),
                    AssigneeUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedByUserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    PlannedStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    PlannedEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "date", nullable: true),
                    ActualEndDate = table.Column<DateTime>(type: "date", nullable: true),
                    Progress = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageTasks", x => x.Id);
                    table.CheckConstraint("CK_StageTasks_Progress", "[Progress] >= 0 AND [Progress] <= 100 AND [Weight] >= 0 AND [Weight] <= 100");
                    table.CheckConstraint("CK_StageTasks_Target", "([TargetType] = 1 AND [DepartmentId] IS NOT NULL AND [OfficeId] IS NULL AND [AssigneeUserId] IS NULL) OR ([TargetType] = 2 AND [OfficeId] IS NOT NULL AND [DepartmentId] IS NULL AND [AssigneeUserId] IS NULL) OR ([TargetType] = 3 AND [AssigneeUserId] IS NOT NULL AND [DepartmentId] IS NULL AND [OfficeId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_StageTasks_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageTasks_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageTasks_ProjectStages_StageId",
                        column: x => x.StageId,
                        principalTable: "ProjectStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StageTasks_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageTasks_Users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArchitecturalVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DrawingId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    VersionDate = table.Column<DateTime>(type: "date", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchitecturalVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchitecturalVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DesignApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VersionId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CdaNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CdaDate = table.Column<DateTime>(type: "date", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TechnicalNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DesignApprovals_ArchitecturalVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ArchitecturalVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DesignApprovals_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectDrawings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    OfficeId = table.Column<int>(type: "int", nullable: true),
                    PreparedByUserId = table.Column<int>(type: "int", nullable: false),
                    BasedOnVersionId = table.Column<int>(type: "int", nullable: true),
                    DrawingKind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DrawingNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DrawingDate = table.Column<DateTime>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDrawings", x => x.Id);
                    table.CheckConstraint("CK_ProjectDrawings_BasedOn", "[DrawingKind] <> 1 OR [BasedOnVersionId] IS NULL");
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_ArchitecturalVersions_BasedOnVersionId",
                        column: x => x.BasedOnVersionId,
                        principalTable: "ArchitecturalVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_StudyProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "StudyProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectDrawings_Users_PreparedByUserId",
                        column: x => x.PreparedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignmentId = table.Column<int>(type: "int", nullable: true),
                    ResponseId = table.Column<int>(type: "int", nullable: true),
                    StartOrderId = table.Column<int>(type: "int", nullable: true),
                    DesignApprovalId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    SignerUserId = table.Column<int>(type: "int", nullable: true),
                    SignatureId = table.Column<int>(type: "int", nullable: true),
                    SignerRole = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyApprovals", x => x.Id);
                    table.CheckConstraint("CK_StudyApprovals_OneOwner", "(CASE WHEN [AssignmentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ResponseId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StartOrderId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [DesignApprovalId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ProjectId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_StudyApprovals_DesignApprovals_DesignApprovalId",
                        column: x => x.DesignApprovalId,
                        principalTable: "DesignApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyApprovals_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyApprovals_StudyAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "StudyAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyApprovals_StudyProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "StudyProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyApprovals_StudyStartOrders_StartOrderId",
                        column: x => x.StartOrderId,
                        principalTable: "StudyStartOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyApprovals_UserSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "UserSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyApprovals_Users_SignerUserId",
                        column: x => x.SignerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<int>(type: "int", nullable: true),
                    AssignmentId = table.Column<int>(type: "int", nullable: true),
                    ResponseId = table.Column<int>(type: "int", nullable: true),
                    StartOrderId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    DrawingId = table.Column<int>(type: "int", nullable: true),
                    VersionId = table.Column<int>(type: "int", nullable: true),
                    AttachmentTypeId = table.Column<int>(type: "int", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyAttachments", x => x.Id);
                    table.CheckConstraint("CK_StudyAttachments_OneOwner", "(CASE WHEN [RequestId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [AssignmentId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ResponseId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [StartOrderId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [ProjectId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [DrawingId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [VersionId] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_StudyAttachments_ArchitecturalVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ArchitecturalVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_ProjectDrawings_DrawingId",
                        column: x => x.DrawingId,
                        principalTable: "ProjectDrawings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_StudyAssignmentResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "StudyAssignmentResponses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_StudyAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "StudyAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_StudyAttachmentTypes_AttachmentTypeId",
                        column: x => x.AttachmentTypeId,
                        principalTable: "StudyAttachmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_StudyProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "StudyProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_StudyRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "StudyRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_StudyStartOrders_StartOrderId",
                        column: x => x.StartOrderId,
                        principalTable: "StudyStartOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudyAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalVersions_CreatedByUserId",
                table: "ArchitecturalVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchitecturalVersions_DrawingId_VersionNumber",
                table: "ArchitecturalVersions",
                columns: new[] { "DrawingId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignApprovals_CdaNumber",
                table: "DesignApprovals",
                column: "CdaNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesignApprovals_CreatedByUserId",
                table: "DesignApprovals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DesignApprovals_VersionId",
                table: "DesignApprovals",
                column: "VersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngineeringDisciplines_Name",
                table: "EngineeringDisciplines",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDepartments_AddedByUserId",
                table: "ProjectDepartments",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDepartments_DepartmentId",
                table: "ProjectDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDepartments_ProjectId_DepartmentId",
                table: "ProjectDepartments",
                columns: new[] { "ProjectId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_BasedOnVersionId",
                table: "ProjectDrawings",
                column: "BasedOnVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_DepartmentId",
                table: "ProjectDrawings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_OfficeId",
                table: "ProjectDrawings",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_PreparedByUserId",
                table: "ProjectDrawings",
                column: "PreparedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectId_DrawingKind",
                table: "ProjectDrawings",
                columns: new[] { "ProjectId", "DrawingKind" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDrawings_ProjectId_DrawingNumber",
                table: "ProjectDrawings",
                columns: new[] { "ProjectId", "DrawingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_AssignedByUserId",
                table: "ProjectMembers",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId",
                table: "ProjectMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_ClosedByUserId",
                table: "ProjectStages",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_ProjectId_SortOrder",
                table: "ProjectStages",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_ProjectId_StageTypeId",
                table: "ProjectStages",
                columns: new[] { "ProjectId", "StageTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_ResponsibleDepartmentId",
                table: "ProjectStages",
                column: "ResponsibleDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStages_StageTypeId",
                table: "ProjectStages",
                column: "StageTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseCostItems_ResponseId_SortOrder",
                table: "ResponseCostItems",
                columns: new[] { "ResponseId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ResponseLogisticItems_ResponseId",
                table: "ResponseLogisticItems",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseStaffEstimates_DisciplineId",
                table: "ResponseStaffEstimates",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_StageTasks_AssignedByUserId",
                table: "StageTasks",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StageTasks_AssigneeUserId",
                table: "StageTasks",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StageTasks_DepartmentId",
                table: "StageTasks",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StageTasks_OfficeId",
                table: "StageTasks",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_StageTasks_StageId",
                table: "StageTasks",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_StartOrderEngineers_DisciplineId",
                table: "StartOrderEngineers",
                column: "DisciplineId");

            migrationBuilder.CreateIndex(
                name: "IX_StartOrderEngineers_EngineerUserId",
                table: "StartOrderEngineers",
                column: "EngineerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StartOrderResources_StartOrderId",
                table: "StartOrderResources",
                column: "StartOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyActivityLogs_RequestId_CreatedAt",
                table: "StudyActivityLogs",
                columns: new[] { "RequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyActivityLogs_UserId",
                table: "StudyActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_AssignmentId_SignerRole",
                table: "StudyApprovals",
                columns: new[] { "AssignmentId", "SignerRole" },
                unique: true,
                filter: "[AssignmentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_DesignApprovalId_SignerRole",
                table: "StudyApprovals",
                columns: new[] { "DesignApprovalId", "SignerRole" },
                unique: true,
                filter: "[DesignApprovalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_ProjectId_SignerRole",
                table: "StudyApprovals",
                columns: new[] { "ProjectId", "SignerRole" },
                unique: true,
                filter: "[ProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_ResponseId_SignerRole",
                table: "StudyApprovals",
                columns: new[] { "ResponseId", "SignerRole" },
                unique: true,
                filter: "[ResponseId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_SignatureId",
                table: "StudyApprovals",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_SignerRole_Decision",
                table: "StudyApprovals",
                columns: new[] { "SignerRole", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_SignerUserId",
                table: "StudyApprovals",
                column: "SignerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyApprovals_StartOrderId_SignerRole",
                table: "StudyApprovals",
                columns: new[] { "StartOrderId", "SignerRole" },
                unique: true,
                filter: "[StartOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignmentResponses_AssignmentId",
                table: "StudyAssignmentResponses",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignmentResponses_CreatedByUserId",
                table: "StudyAssignmentResponses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignmentResponses_ProposedManagerUserId",
                table: "StudyAssignmentResponses",
                column: "ProposedManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignmentResponses_Status_CreatedAt",
                table: "StudyAssignmentResponses",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignments_AssignmentNumber",
                table: "StudyAssignments",
                column: "AssignmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignments_CreatedByUserId",
                table: "StudyAssignments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignments_FollowingDepartmentId",
                table: "StudyAssignments",
                column: "FollowingDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignments_ProjectCode",
                table: "StudyAssignments",
                column: "ProjectCode",
                unique: true,
                filter: "[ProjectCode] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignments_RequestId",
                table: "StudyAssignments",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignments_Status_CreatedAt",
                table: "StudyAssignments",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_AssignmentId",
                table: "StudyAttachments",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_AttachmentTypeId",
                table: "StudyAttachments",
                column: "AttachmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_DrawingId",
                table: "StudyAttachments",
                column: "DrawingId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_ProjectId",
                table: "StudyAttachments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_RequestId",
                table: "StudyAttachments",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_ResponseId",
                table: "StudyAttachments",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_StartOrderId",
                table: "StudyAttachments",
                column: "StartOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_StorageKey",
                table: "StudyAttachments",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_UploadedByUserId",
                table: "StudyAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachments_VersionId",
                table: "StudyAttachments",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAttachmentTypes_Name",
                table: "StudyAttachmentTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_BranchId_Status_CreatedAt",
                table: "StudyProjects",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_Code",
                table: "StudyProjects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_CreatedByUserId",
                table: "StudyProjects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_ManagerUserId",
                table: "StudyProjects",
                column: "ManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyProjects_StartOrderId",
                table: "StudyProjects",
                column: "StartOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequests_BranchId_Status_CreatedAt",
                table: "StudyRequests",
                columns: new[] { "BranchId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequests_CreatedByUserId",
                table: "StudyRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequests_MainBeneficiary",
                table: "StudyRequests",
                column: "MainBeneficiary");

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequests_RequestNumber",
                table: "StudyRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequests_SiteId",
                table: "StudyRequests",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyStageTypes_Name",
                table: "StudyStageTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyStartOrders_CompetentDepartmentId",
                table: "StudyStartOrders",
                column: "CompetentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyStartOrders_CreatedByUserId",
                table: "StudyStartOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyStartOrders_OrderNumber",
                table: "StudyStartOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyStartOrders_ProjectManagerUserId",
                table: "StudyStartOrders",
                column: "ProjectManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyStartOrders_ResponseId",
                table: "StudyStartOrders",
                column: "ResponseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyStartOrders_Status_CreatedAt",
                table: "StudyStartOrders",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ArchitecturalVersions_ProjectDrawings_DrawingId",
                table: "ArchitecturalVersions",
                column: "DrawingId",
                principalTable: "ProjectDrawings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchitecturalVersions_ProjectDrawings_DrawingId",
                table: "ArchitecturalVersions");

            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropTable(
                name: "ResponseBuildings");

            migrationBuilder.DropTable(
                name: "ResponseCostItems");

            migrationBuilder.DropTable(
                name: "ResponseLogisticItems");

            migrationBuilder.DropTable(
                name: "ResponseStaffEstimates");

            migrationBuilder.DropTable(
                name: "StageTasks");

            migrationBuilder.DropTable(
                name: "StartOrderEngineers");

            migrationBuilder.DropTable(
                name: "StartOrderResources");

            migrationBuilder.DropTable(
                name: "StudyActivityLogs");

            migrationBuilder.DropTable(
                name: "StudyApprovals");

            migrationBuilder.DropTable(
                name: "StudyAttachments");

            migrationBuilder.DropTable(
                name: "StudyNumberSequences");

            migrationBuilder.DropTable(
                name: "ProjectDepartments");

            migrationBuilder.DropTable(
                name: "ProjectStages");

            migrationBuilder.DropTable(
                name: "EngineeringDisciplines");

            migrationBuilder.DropTable(
                name: "DesignApprovals");

            migrationBuilder.DropTable(
                name: "StudyAttachmentTypes");

            migrationBuilder.DropTable(
                name: "StudyStageTypes");

            migrationBuilder.DropTable(
                name: "ProjectDrawings");

            migrationBuilder.DropTable(
                name: "ArchitecturalVersions");

            migrationBuilder.DropTable(
                name: "StudyProjects");

            migrationBuilder.DropTable(
                name: "StudyStartOrders");

            migrationBuilder.DropTable(
                name: "StudyAssignmentResponses");

            migrationBuilder.DropTable(
                name: "StudyAssignments");

            migrationBuilder.DropTable(
                name: "StudyRequests");
        }
    }
}
