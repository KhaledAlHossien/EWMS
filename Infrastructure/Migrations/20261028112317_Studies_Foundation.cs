using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Studies_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliverableTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentKind = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliverableTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliverableTypes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    CorrespondenceId = table.Column<int>(type: "int", nullable: true),
                    ExternalNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyOrders_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyOrders_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyOrders_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudyDeliverables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudyOrderId = table.Column<int>(type: "int", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    OfficeId = table.Column<int>(type: "int", nullable: true),
                    AssigneeUserId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateTime>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<int>(type: "int", nullable: true),
                    SubmittedVersionId = table.Column<int>(type: "int", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "int", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedVersionId = table.Column<int>(type: "int", nullable: true),
                    OverdueNotifiedFor = table.Column<DateTime>(type: "date", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyDeliverables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_DeliverableTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "DeliverableTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_Offices_OfficeId",
                        column: x => x.OfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_ProjectDocumentVersions_ApprovedVersionId",
                        column: x => x.ApprovedVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_ProjectDocumentVersions_SubmittedVersionId",
                        column: x => x.SubmittedVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_ProjectDocuments_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "ProjectDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_StudyOrders_StudyOrderId",
                        column: x => x.StudyOrderId,
                        principalTable: "StudyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_Users_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudyDeliverables_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudySubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StudyOrderId = table.Column<int>(type: "int", nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentByUserId = table.Column<int>(type: "int", nullable: false),
                    OutgoingCorrespondenceId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecisionVersionId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudySubmissions_Correspondences_OutgoingCorrespondenceId",
                        column: x => x.OutgoingCorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudySubmissions_ProjectDocumentVersions_DecisionVersionId",
                        column: x => x.DecisionVersionId,
                        principalTable: "ProjectDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudySubmissions_StudyOrders_StudyOrderId",
                        column: x => x.StudyOrderId,
                        principalTable: "StudyOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudySubmissions_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudySubmissions_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliverableTypes_BranchId_Name",
                table: "DeliverableTypes",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_ApprovedVersionId",
                table: "StudyDeliverables",
                column: "ApprovedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_AssigneeUserId",
                table: "StudyDeliverables",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_DocumentId",
                table: "StudyDeliverables",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_OfficeId",
                table: "StudyDeliverables",
                column: "OfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_ReviewedByUserId",
                table: "StudyDeliverables",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_StudyOrderId_TypeId",
                table: "StudyDeliverables",
                columns: new[] { "StudyOrderId", "TypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_SubmittedByUserId",
                table: "StudyDeliverables",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_SubmittedVersionId",
                table: "StudyDeliverables",
                column: "SubmittedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyDeliverables_TypeId",
                table: "StudyDeliverables",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyOrders_CorrespondenceId",
                table: "StudyOrders",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyOrders_CreatedByUserId",
                table: "StudyOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyOrders_ProjectId_Status",
                table: "StudyOrders",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudySubmissions_DecidedByUserId",
                table: "StudySubmissions",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySubmissions_DecisionVersionId",
                table: "StudySubmissions",
                column: "DecisionVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySubmissions_OnePending",
                table: "StudySubmissions",
                column: "StudyOrderId",
                unique: true,
                filter: "[Decision] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StudySubmissions_OutgoingCorrespondenceId",
                table: "StudySubmissions",
                column: "OutgoingCorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySubmissions_SentByUserId",
                table: "StudySubmissions",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudySubmissions_StudyOrderId_Round",
                table: "StudySubmissions",
                columns: new[] { "StudyOrderId", "Round" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudyDeliverables");

            migrationBuilder.DropTable(
                name: "StudySubmissions");

            migrationBuilder.DropTable(
                name: "DeliverableTypes");

            migrationBuilder.DropTable(
                name: "StudyOrders");
        }
    }
}
