using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Project_Schedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSchedules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSchedules_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSchedules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectStartOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OrderDate = table.Column<DateTime>(type: "date", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    CompletionDays = table.Column<int>(type: "int", nullable: false),
                    ExecutionDays = table.Column<int>(type: "int", nullable: false),
                    StudyCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SiteEngineerUserId = table.Column<int>(type: "int", nullable: true),
                    SectorSupervisorUserId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStartOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectStartOrders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectStartOrders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectStartOrders_Users_SectorSupervisorUserId",
                        column: x => x.SectorSupervisorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectStartOrders_Users_SiteEngineerUserId",
                        column: x => x.SiteEngineerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PlannedStart = table.Column<DateTime>(type: "date", nullable: false),
                    PlannedEnd = table.Column<DateTime>(type: "date", nullable: false),
                    OriginalStart = table.Column<DateTime>(type: "date", nullable: true),
                    OriginalEnd = table.Column<DateTime>(type: "date", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    ActualStart = table.Column<DateTime>(type: "date", nullable: true),
                    ActualEnd = table.Column<DateTime>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AssigneeUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleTasks", x => x.Id);
                    table.CheckConstraint("CK_ScheduleTasks_Dates", "[PlannedEnd] >= [PlannedStart]");
                    table.CheckConstraint("CK_ScheduleTasks_Progress", "[Progress] >= 0 AND [Progress] <= 100");
                    table.CheckConstraint("CK_ScheduleTasks_Weight", "[Weight] > 0");
                    table.ForeignKey(
                        name: "FK_ScheduleTasks_ProjectSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ProjectSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleTasks_Users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleDelays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "date", nullable: false),
                    PlannedEndAtDiscovery = table.Column<DateTime>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Actions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ApprovalDocumentVersionId = table.Column<int>(type: "int", nullable: true),
                    ApprovalAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApprovalDate = table.Column<DateTime>(type: "date", nullable: true),
                    AffectsCriticalWork = table.Column<bool>(type: "bit", nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleDelays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleDelays_ProjectDocumentVersions_ApprovalDocumentVersionId",
                        column: x => x.ApprovalDocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleDelays_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleDelays_ScheduleTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ScheduleTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleDelays_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleDelays_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    PredecessorId = table.Column<int>(type: "int", nullable: false),
                    SuccessorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleDependencies", x => x.Id);
                    table.CheckConstraint("CK_ScheduleDependencies_NotSelf", "[PredecessorId] <> [SuccessorId]");
                    table.ForeignKey(
                        name: "FK_ScheduleDependencies_ProjectSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ProjectSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleDependencies_ScheduleTasks_PredecessorId",
                        column: x => x.PredecessorId,
                        principalTable: "ScheduleTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleDependencies_ScheduleTasks_SuccessorId",
                        column: x => x.SuccessorId,
                        principalTable: "ScheduleTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleProgressEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleProgressEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleProgressEntries_ScheduleTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ScheduleTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleProgressEntries_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleDelayAffectedTasks",
                columns: table => new
                {
                    DelayId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleDelayAffectedTasks", x => new { x.DelayId, x.TaskId });
                    table.ForeignKey(
                        name: "FK_ScheduleDelayAffectedTasks_ScheduleDelays_DelayId",
                        column: x => x.DelayId,
                        principalTable: "ScheduleDelays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleDelayAffectedTasks_ScheduleTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ScheduleTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleExtensions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<int>(type: "int", nullable: false),
                    DelayId = table.Column<int>(type: "int", nullable: true),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    DayCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensions_ProjectSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "ProjectSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensions_ScheduleDelays_DelayId",
                        column: x => x.DelayId,
                        principalTable: "ScheduleDelays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensions_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensions_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleExtensionDocuments",
                columns: table => new
                {
                    ExtensionId = table.Column<int>(type: "int", nullable: false),
                    DocumentVersionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleExtensionDocuments", x => new { x.ExtensionId, x.DocumentVersionId });
                    table.ForeignKey(
                        name: "FK_ScheduleExtensionDocuments_ProjectDocumentVersions_DocumentVersionId",
                        column: x => x.DocumentVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensionDocuments_ScheduleExtensions_ExtensionId",
                        column: x => x.ExtensionId,
                        principalTable: "ScheduleExtensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleExtensionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExtensionId = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    MovesStart = table.Column<bool>(type: "bit", nullable: false),
                    OldStart = table.Column<DateTime>(type: "date", nullable: false),
                    OldEnd = table.Column<DateTime>(type: "date", nullable: false),
                    NewStart = table.Column<DateTime>(type: "date", nullable: false),
                    NewEnd = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleExtensionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensionItems_ScheduleExtensions_ExtensionId",
                        column: x => x.ExtensionId,
                        principalTable: "ScheduleExtensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduleExtensionItems_ScheduleTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "ScheduleTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSchedules_ApprovedByUserId",
                table: "ProjectSchedules",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSchedules_CreatedByUserId",
                table: "ProjectSchedules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSchedules_OneDraftOneApproved",
                table: "ProjectSchedules",
                columns: new[] { "ProjectId", "Status" },
                unique: true,
                filter: "[Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSchedules_ProjectId_VersionNumber",
                table: "ProjectSchedules",
                columns: new[] { "ProjectId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStartOrders_CreatedByUserId",
                table: "ProjectStartOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStartOrders_ProjectId",
                table: "ProjectStartOrders",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStartOrders_SectorSupervisorUserId",
                table: "ProjectStartOrders",
                column: "SectorSupervisorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectStartOrders_SiteEngineerUserId",
                table: "ProjectStartOrders",
                column: "SiteEngineerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDelayAffectedTasks_TaskId",
                table: "ScheduleDelayAffectedTasks",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDelays_ApprovalDocumentVersionId",
                table: "ScheduleDelays",
                column: "ApprovalDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDelays_DecidedByUserId",
                table: "ScheduleDelays",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDelays_ProjectId_Status",
                table: "ScheduleDelays",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDelays_RecordedByUserId",
                table: "ScheduleDelays",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDelays_TaskId",
                table: "ScheduleDelays",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDependencies_PredecessorId_SuccessorId",
                table: "ScheduleDependencies",
                columns: new[] { "PredecessorId", "SuccessorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDependencies_ScheduleId",
                table: "ScheduleDependencies",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleDependencies_SuccessorId",
                table: "ScheduleDependencies",
                column: "SuccessorId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensionDocuments_DocumentVersionId",
                table: "ScheduleExtensionDocuments",
                column: "DocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensionItems_ExtensionId_TaskId",
                table: "ScheduleExtensionItems",
                columns: new[] { "ExtensionId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensionItems_TaskId",
                table: "ScheduleExtensionItems",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensions_DecidedByUserId",
                table: "ScheduleExtensions",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensions_DelayId",
                table: "ScheduleExtensions",
                column: "DelayId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensions_ProjectId_Status",
                table: "ScheduleExtensions",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensions_RequestedByUserId",
                table: "ScheduleExtensions",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleExtensions_ScheduleId",
                table: "ScheduleExtensions",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleProgressEntries_TaskId_CreatedAt",
                table: "ScheduleProgressEntries",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleProgressEntries_UserId",
                table: "ScheduleProgressEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTasks_AssigneeUserId",
                table: "ScheduleTasks",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleTasks_ScheduleId_SortOrder",
                table: "ScheduleTasks",
                columns: new[] { "ScheduleId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectStartOrders");

            migrationBuilder.DropTable(
                name: "ScheduleDelayAffectedTasks");

            migrationBuilder.DropTable(
                name: "ScheduleDependencies");

            migrationBuilder.DropTable(
                name: "ScheduleExtensionDocuments");

            migrationBuilder.DropTable(
                name: "ScheduleExtensionItems");

            migrationBuilder.DropTable(
                name: "ScheduleProgressEntries");

            migrationBuilder.DropTable(
                name: "ScheduleExtensions");

            migrationBuilder.DropTable(
                name: "ScheduleDelays");

            migrationBuilder.DropTable(
                name: "ScheduleTasks");

            migrationBuilder.DropTable(
                name: "ProjectSchedules");
        }
    }
}
