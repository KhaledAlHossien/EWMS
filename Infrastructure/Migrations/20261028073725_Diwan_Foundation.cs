using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Diwan_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrespondenceStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceStatuses_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeadlineDays = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceTypes_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Correspondences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: true),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: true),
                    ExternalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExternalDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Party = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: true),
                    ProjectId = table.Column<int>(type: "int", nullable: true),
                    CurrentDepartmentId = table.Column<int>(type: "int", nullable: true),
                    CurrentOfficeId = table.Column<int>(type: "int", nullable: true),
                    CurrentUserId = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueSoonNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OverdueNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OutgoingState = table.Column<int>(type: "int", nullable: true),
                    ReplyToId = table.Column<int>(type: "int", nullable: true),
                    ReturnNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedSignatureId = table.Column<int>(type: "int", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssuedByUserId = table.Column<int>(type: "int", nullable: true),
                    RegisteredByUserId = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByUserId = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correspondences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Correspondences_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_CorrespondenceStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "CorrespondenceStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_CorrespondenceTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "CorrespondenceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Correspondences_ReplyToId",
                        column: x => x.ReplyToId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Departments_CurrentDepartmentId",
                        column: x => x.CurrentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Offices_CurrentOfficeId",
                        column: x => x.CurrentOfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_UserSignatures_ApprovedSignatureId",
                        column: x => x.ApprovedSignatureId,
                        principalTable: "UserSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Users_CurrentUserId",
                        column: x => x.CurrentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrespondenceId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceAttachments_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceReferrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrespondenceId = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<int>(type: "int", nullable: false),
                    FromDepartmentId = table.Column<int>(type: "int", nullable: true),
                    ToDepartmentId = table.Column<int>(type: "int", nullable: false),
                    ToOfficeId = table.Column<int>(type: "int", nullable: true),
                    ToUserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceReferrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReferrals_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReferrals_Departments_FromDepartmentId",
                        column: x => x.FromDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReferrals_Departments_ToDepartmentId",
                        column: x => x.ToDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReferrals_Offices_ToOfficeId",
                        column: x => x.ToOfficeId,
                        principalTable: "Offices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReferrals_Users_AcknowledgedByUserId",
                        column: x => x.AcknowledgedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReferrals_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReferrals_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceAttachmentContents",
                columns: table => new
                {
                    AttachmentId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceAttachmentContents", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_CorrespondenceAttachmentContents_CorrespondenceAttachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "CorrespondenceAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceAttachments_CorrespondenceId",
                table: "CorrespondenceAttachments",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceAttachments_StorageKey",
                table: "CorrespondenceAttachments",
                column: "StorageKey",
                unique: true,
                filter: "[StorageKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceAttachments_UploadedByUserId",
                table: "CorrespondenceAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReferrals_AcknowledgedByUserId",
                table: "CorrespondenceReferrals",
                column: "AcknowledgedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReferrals_CorrespondenceId",
                table: "CorrespondenceReferrals",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReferrals_FromDepartmentId",
                table: "CorrespondenceReferrals",
                column: "FromDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReferrals_FromUserId",
                table: "CorrespondenceReferrals",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReferrals_ToDepartmentId",
                table: "CorrespondenceReferrals",
                column: "ToDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReferrals_ToOfficeId",
                table: "CorrespondenceReferrals",
                column: "ToOfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReferrals_ToUserId",
                table: "CorrespondenceReferrals",
                column: "ToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ApprovedByUserId",
                table: "Correspondences",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ApprovedSignatureId",
                table: "Correspondences",
                column: "ApprovedSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_BranchId_Direction_RegisteredAt",
                table: "Correspondences",
                columns: new[] { "BranchId", "Direction", "RegisteredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_BranchId_Direction_Year_Sequence",
                table: "Correspondences",
                columns: new[] { "BranchId", "Direction", "Year", "Sequence" },
                unique: true,
                filter: "[Sequence] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ClosedByUserId",
                table: "Correspondences",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_CurrentDepartmentId",
                table: "Correspondences",
                column: "CurrentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_CurrentOfficeId",
                table: "Correspondences",
                column: "CurrentOfficeId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_CurrentUserId",
                table: "Correspondences",
                column: "CurrentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_DepartmentId",
                table: "Correspondences",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_DueDate",
                table: "Correspondences",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_IssuedByUserId",
                table: "Correspondences",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ProjectId",
                table: "Correspondences",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_RegisteredByUserId",
                table: "Correspondences",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ReplyToId",
                table: "Correspondences",
                column: "ReplyToId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_StatusId",
                table: "Correspondences",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_TypeId",
                table: "Correspondences",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceStatuses_BranchId_Name",
                table: "CorrespondenceStatuses",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceTypes_BranchId_Name",
                table: "CorrespondenceTypes",
                columns: new[] { "BranchId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrespondenceAttachmentContents");

            migrationBuilder.DropTable(
                name: "CorrespondenceReferrals");

            migrationBuilder.DropTable(
                name: "CorrespondenceAttachments");

            migrationBuilder.DropTable(
                name: "Correspondences");

            migrationBuilder.DropTable(
                name: "CorrespondenceStatuses");

            migrationBuilder.DropTable(
                name: "CorrespondenceTypes");
        }
    }
}
