using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// رقم المتابع في طلب تجهيز الدراسة (2026-11-28): نص من أحرف وأرقام بدل رقم الهاتف السوري، فاتّسع العمود من 20 إلى 50.
    /// القيم الموجودة (هواتف) تبقى كما هي وصالحة. Down يقصّ ما زاد على 20 حرفاً قبل التضييق حتى لا يفشل.
    /// </summary>
    public partial class Study_Request_Follower_Number_Text : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FollowerPhone",
                table: "StudyRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [StudyRequests] SET [FollowerPhone] = LEFT([FollowerPhone], 20) WHERE LEN([FollowerPhone]) > 20;");

            migrationBuilder.AlterColumn<string>(
                name: "FollowerPhone",
                table: "StudyRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
