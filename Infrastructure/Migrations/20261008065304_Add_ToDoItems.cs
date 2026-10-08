using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_ToDoItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ToDoItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ToDoListId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToDoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToDoItems_ToDoLists_ToDoListId",
                        column: x => x.ToDoListId,
                        principalTable: "ToDoLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToDoItems_ToDoListId_SortOrder",
                table: "ToDoItems",
                columns: new[] { "ToDoListId", "SortOrder" });

            // قوائم المهام الشخصية (قرار المستخدم 2026-10-08): صلاحياتها الأربع تُمنح لكل الأدوار الحالية لأنها شخصية الحدّ (قوائمه هو فقط).
            // نُدرج صفوف الصلاحيات أولاً إن لم تكن موجودة (الـ seeder يعمل بعد الترحيل عند الإقلاع) ثم نمنحها.
            // الأدوار التي تُنشأ لاحقاً من صفحة الأدوار لا تحصل عليها تلقائياً.
            migrationBuilder.Sql(@"
INSERT INTO Permissions (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
  (N'ViewToDoLists',  N'عرض قوائم مهامه الشخصية'),
  (N'CreateToDoList', N'إضافة قائمة مهام شخصية'),
  (N'EditToDoList',   N'تعديل قوائم مهامه الشخصية'),
  (N'DeleteToDoList', N'حذف قوائم مهامه الشخصية')) AS v(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM Permissions p WHERE p.Name = v.Name);

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM Roles r
CROSS JOIN Permissions p
WHERE p.Name IN (N'ViewToDoLists', N'CreateToDoList', N'EditToDoList', N'DeleteToDoList')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ToDoItems");
        }
    }
}
