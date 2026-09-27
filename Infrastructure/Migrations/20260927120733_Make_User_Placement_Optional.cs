using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Make_User_Placement_Optional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server لا يسمح بتعديل عمود مفهرس — نحذف الفهارس ثم نعيد إنشاءها بعد التعديل
            migrationBuilder.DropIndex(name: "IX_Users_OfficeId", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_DepartmentId", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_BranchId", table: "Users");

            migrationBuilder.AlterColumn<int>(
                name: "OfficeId",
                table: "Users",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Users",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Users",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(name: "IX_Users_OfficeId", table: "Users", column: "OfficeId");
            migrationBuilder.CreateIndex(name: "IX_Users_DepartmentId", table: "Users", column: "DepartmentId");
            migrationBuilder.CreateIndex(name: "IX_Users_BranchId", table: "Users", column: "BranchId");

            // تنظيف البيانات الحالية لتطابق قاعدة التبعية حسب الدور (UserPlacement):
            // SuperAdmin بلا فرع/قسم/مكتب، رئيس الفرع بلا قسم/مكتب، رئيس القسم بلا مكتب
            migrationBuilder.Sql(@"
UPDATE u SET u.BranchId = NULL, u.DepartmentId = NULL, u.OfficeId = NULL
FROM Users u INNER JOIN Roles r ON r.Id = u.RoleId WHERE r.Name = N'SuperAdmin';

UPDATE u SET u.DepartmentId = NULL, u.OfficeId = NULL
FROM Users u INNER JOIN Roles r ON r.Id = u.RoleId WHERE r.Name = N'BranchManager';

UPDATE u SET u.OfficeId = NULL
FROM Users u INNER JOIN Roles r ON r.Id = u.RoleId WHERE r.Name = N'Manager';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQL Server لا يسمح بتعديل عمود مفهرس — نحذف الفهارس ثم نعيد إنشاءها بعد التعديل
            migrationBuilder.DropIndex(name: "IX_Users_OfficeId", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_DepartmentId", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_BranchId", table: "Users");

            // تنبيه: الرجوع يتطلب أولاً تعيين فرع/قسم/مكتب لكل مستخدم قيمه NULL،
            // وإلا ستفشل قيود المفاتيح الأجنبية عند تحويل الأعمدة إلى NOT NULL بقيمة 0.
            migrationBuilder.AlterColumn<int>(
                name: "OfficeId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DepartmentId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(name: "IX_Users_OfficeId", table: "Users", column: "OfficeId");
            migrationBuilder.CreateIndex(name: "IX_Users_DepartmentId", table: "Users", column: "DepartmentId");
            migrationBuilder.CreateIndex(name: "IX_Users_BranchId", table: "Users", column: "BranchId");
        }
    }
}
