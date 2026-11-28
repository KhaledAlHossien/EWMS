using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class User_Token_Hash : Migration
    {
        /// <summary>
        /// UserTokens يحفظ بصمة SHA-256 للرمز بدل الرمز. البصمة تُحسب في SQL للجلسات القائمة (مطابقة لـ TokenHash.Of:
        /// الرمز نص ASCII، فبايتات varchar = بايتات UTF-8)، فلا يحتاج أحد لإعادة الدخول. الجلسات المنتهية أو المُبطلة تُحذف،
        /// ونسخ البصمة المكررة (رمز حُفظ مرتين) تبقى أحدثها، ثم الفهرس الفريد ويُحذف عمود الرمز.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "UserTokens",
                type: "char(64)",
                nullable: true);

            // EXEC: العمود الجديد يُضاف في الدفعة نفسها حين يُطبَّق الترحيل بسكربت (dotnet ef migrations script)
            migrationBuilder.Sql("""
                DELETE FROM UserTokens WHERE IsRevoked = 1 OR ExpiresAt < SYSUTCDATETIME();
                EXEC(N'UPDATE UserTokens SET TokenHash = LOWER(CONVERT(char(64), HASHBYTES(''SHA2_256'', CONVERT(varchar(max), Token)), 2));
                       WITH d AS (SELECT Id, ROW_NUMBER() OVER (PARTITION BY TokenHash ORDER BY Id DESC) AS n FROM UserTokens)
                       DELETE FROM d WHERE n > 1;');
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "UserTokens",
                type: "char(64)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokens_TokenHash",
                table: "UserTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.DropColumn(
                name: "Token",
                table: "UserTokens");
        }

        /// <summary>البصمة لا تُعكس إلى رمز: الرجوع يحذف الجلسات كلها (يدخل المستخدمون من جديد)</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTokens_TokenHash",
                table: "UserTokens");

            migrationBuilder.Sql("DELETE FROM UserTokens;");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "UserTokens");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "UserTokens",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
