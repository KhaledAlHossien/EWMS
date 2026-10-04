using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class User_PhoneNumber_Text : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // كان عدداً فضاع الصفر الأول: 0 = لا رقم → NULL، و9 خانات (9XXXXXXXX) = جوال 09XXXXXXXX → يُعاد الصفر
            migrationBuilder.Sql(@"
UPDATE Users SET PhoneNumber = NULL WHERE PhoneNumber IN (N'0', N'') OR PhoneNumber LIKE N'-%';
UPDATE Users SET PhoneNumber = N'0' + PhoneNumber WHERE LEN(PhoneNumber) = 9;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // الرجوع إلى عدد: ما لا يتحول إلى رقم يصبح 0
            migrationBuilder.Sql("UPDATE Users SET PhoneNumber = N'0' WHERE PhoneNumber IS NULL OR TRY_CAST(PhoneNumber AS int) IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "PhoneNumber",
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
