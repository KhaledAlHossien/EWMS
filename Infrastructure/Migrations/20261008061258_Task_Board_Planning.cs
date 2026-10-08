using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Task_Board_Planning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnCount",
                table: "AssignedTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AssignedTaskLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssignedTaskId = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTaskLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignedTaskLinks_AssignedTasks_AssignedTaskId",
                        column: x => x.AssignedTaskId,
                        principalTable: "AssignedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignedTaskLinks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignedTaskTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    DefaultDueDays = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTaskTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignedTaskTemplates_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignedTaskRecurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnerUserId = table.Column<int>(type: "int", nullable: false),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    TargetType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    DueAfterDays = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    NextRunDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTaskId = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTaskRecurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignedTaskRecurrences_AssignedTaskTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AssignedTaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignedTaskRecurrences_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignedTaskTemplateItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignedTaskTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignedTaskTemplateItems_AssignedTaskTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AssignedTaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskLinks_AssignedTaskId_EntityType_EntityId",
                table: "AssignedTaskLinks",
                columns: new[] { "AssignedTaskId", "EntityType", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskLinks_CreatedByUserId",
                table: "AssignedTaskLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskLinks_EntityType_EntityId",
                table: "AssignedTaskLinks",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskRecurrences_IsActive_NextRunDate",
                table: "AssignedTaskRecurrences",
                columns: new[] { "IsActive", "NextRunDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskRecurrences_OwnerUserId",
                table: "AssignedTaskRecurrences",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskRecurrences_TemplateId",
                table: "AssignedTaskRecurrences",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskTemplateItems_TemplateId_SortOrder",
                table: "AssignedTaskTemplateItems",
                columns: new[] { "TemplateId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignedTaskTemplates_OwnerUserId_Name",
                table: "AssignedTaskTemplates",
                columns: new[] { "OwnerUserId", "Name" },
                unique: true);

            // صلاحية إحصائيات المهام (2026-10-08): تُمنح لكل دور يملك صلاحية إسناد (AssignTaskTo*) كي لا يفقد المُسنِدون صفحة المتابعة الجديدة.
            // السوبر ادمن يأخذها من الـ seeder. الأدوار التي تتولى المهام فقط لا تحصل عليها.
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT N'ViewTaskStats', N'عرض إحصائيات المهام ضمن نطاقه (الإنجاز في الموعد والمتأخرات لكل جهة)'
WHERE NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = N'ViewTaskStats');

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT DISTINCT r.Id, vp.Id
FROM Roles r
INNER JOIN Permissions vp ON vp.Name = N'ViewTaskStats'
WHERE EXISTS (SELECT 1 FROM RolePermissions rp
              INNER JOIN Permissions p ON p.Id = rp.PermissionId
              WHERE rp.RoleId = r.Id
                AND p.Name IN (N'AssignTaskToDepartment', N'AssignTaskToOffice', N'AssignTaskToUser'))
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = vp.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM Permissions WHERE Name = N'ViewTaskStats';");

            migrationBuilder.DropTable(
                name: "AssignedTaskLinks");

            migrationBuilder.DropTable(
                name: "AssignedTaskRecurrences");

            migrationBuilder.DropTable(
                name: "AssignedTaskTemplateItems");

            migrationBuilder.DropTable(
                name: "AssignedTaskTemplates");

            migrationBuilder.DropColumn(
                name: "ReturnCount",
                table: "AssignedTasks");
        }
    }
}
