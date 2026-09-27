using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_User_OfficeId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) نضيف العمود مبدئياً Nullable لأن جدول المستخدمين قد يحتوي بيانات موجودة
            migrationBuilder.AddColumn<int>(
                name: "OfficeId",
                table: "Users",
                type: "int",
                nullable: true);

            // 2) لأي قسم يحتوي مستخدمين ولا يملك أي مكتب بعد، ننشئ "مكتب افتراضي"
            //    كي لا تنكسر بيانات المستخدمين الحاليين عند تفعيل القيد NOT NULL
            migrationBuilder.Sql(@"
                INSERT INTO Offices (Name, Description, DepartmentId)
                SELECT DISTINCT
                    N'مكتب افتراضي - ' + d.Name,
                    N'مكتب تم إنشاؤه تلقائياً عند إضافة المكاتب للنظام',
                    d.Id
                FROM Departments d
                INNER JOIN Users u ON u.DepartmentId = d.Id
                WHERE NOT EXISTS (SELECT 1 FROM Offices o WHERE o.DepartmentId = d.Id);
            ");

            // 3) نربط كل مستخدم موجود بمكتب قسمه (أول مكتب متاح لنفس القسم)
            migrationBuilder.Sql(@"
                UPDATE u
                SET u.OfficeId = o.Id
                FROM Users u
                CROSS APPLY (
                    SELECT TOP 1 Id FROM Offices WHERE DepartmentId = u.DepartmentId
                ) o
                WHERE u.OfficeId IS NULL;
            ");

            // 4) الآن بعد أن أصبح لكل مستخدم مكتب، نفعّل القيد NOT NULL
            migrationBuilder.AlterColumn<int>(
                name: "OfficeId",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_OfficeId",
                table: "Users",
                column: "OfficeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Offices_OfficeId",
                table: "Users",
                column: "OfficeId",
                principalTable: "Offices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Offices_OfficeId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_OfficeId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OfficeId",
                table: "Users");
        }
    }
}
