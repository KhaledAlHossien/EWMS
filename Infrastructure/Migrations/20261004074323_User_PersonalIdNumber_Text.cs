using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class User_PersonalIdNumber_Text : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PersonalIdNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // كان رقماً لا يُدخل من أي نموذج، فكل الحسابات تحمل 0 → يصبح فارغاً (NULL) قبل إنشاء الفهرس الفريد.
            // وأي رقم مكرر (إن وُجد) يُفرَّغ أيضاً كي لا يفشل الفهرس — يُعاد إدخاله من صفحة المستخدمين.
            migrationBuilder.Sql(@"
UPDATE Users SET PersonalIdNumber = NULL WHERE PersonalIdNumber IN (N'0', N'') OR PersonalIdNumber LIKE N'-%';
UPDATE Users SET PersonalIdNumber = NULL
WHERE PersonalIdNumber IN (SELECT PersonalIdNumber FROM Users WHERE PersonalIdNumber IS NOT NULL GROUP BY PersonalIdNumber HAVING COUNT(*) > 1);");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PersonalIdNumber",
                table: "Users",
                column: "PersonalIdNumber",
                unique: true,
                filter: "[PersonalIdNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PersonalIdNumber",
                table: "Users");

            // الرجوع: النصوص غير الرقمية تُفقد (تصبح 0)
            migrationBuilder.Sql("UPDATE Users SET PersonalIdNumber = N'0' WHERE PersonalIdNumber IS NULL OR TRY_CAST(PersonalIdNumber AS int) IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "PersonalIdNumber",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }
    }
}
