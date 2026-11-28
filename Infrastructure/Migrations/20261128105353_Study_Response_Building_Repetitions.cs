using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// إشعار الرد (2026-11-28): ملاحظة بجانب الموقع العام، وملاحظة وعدد تكرارات لكل مبنى ولبند تكلفته،
    /// ونوع البناء الثالث «إعادة تأهيل جزئي» (القيمة 3، بلا تغيير في الجداول).
    /// معدّل يدوياً: القيمة الافتراضية للتكرار 1 لا 0، فالأسطر الموجودة تكرارها 1 فتبقى مساحاتها وكلفها كما هي،
    /// ولا يخالف أيٌّ منها القيد [Repetitions] >= 1.
    /// </summary>
    public partial class Study_Response_Building_Repetitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SiteNote",
                table: "StudyAssignmentResponses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repetitions",
                table: "ResponseCostItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ResponseBuildingDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Repetitions",
                table: "ResponseBuildingDetails",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ResponseCostItems_Repetitions",
                table: "ResponseCostItems",
                sql: "[Repetitions] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ResponseBuildingDetails_Repetitions",
                table: "ResponseBuildingDetails",
                sql: "[Repetitions] >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ResponseCostItems_Repetitions",
                table: "ResponseCostItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ResponseBuildingDetails_Repetitions",
                table: "ResponseBuildingDetails");

            migrationBuilder.DropColumn(
                name: "SiteNote",
                table: "StudyAssignmentResponses");

            migrationBuilder.DropColumn(
                name: "Repetitions",
                table: "ResponseCostItems");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ResponseBuildingDetails");

            migrationBuilder.DropColumn(
                name: "Repetitions",
                table: "ResponseBuildingDetails");
        }
    }
}
