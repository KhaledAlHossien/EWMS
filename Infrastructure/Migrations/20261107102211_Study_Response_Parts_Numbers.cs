using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Response_Parts_Numbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiwanRequestNumber",
                table: "StudyRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialCost",
                table: "StudyAssignmentResponses",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseNumber",
                table: "StudyAssignmentResponses",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SiteArea",
                table: "StudyAssignmentResponses",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            // مساحة الموقع والتكلفة المبدئية صارتا حقلاً واحداً للرد: التكلفة مجموع سطور المباني، والمساحة أكبر قيمة
            migrationBuilder.Sql(@"
                UPDATE r SET r.[InitialCost] = x.[Cost], r.[SiteArea] = x.[Area]
                FROM [StudyAssignmentResponses] r
                JOIN (SELECT [ResponseId], SUM([InitialCost]) AS [Cost], MAX([SiteArea]) AS [Area]
                      FROM [ResponseBuildings] GROUP BY [ResponseId]) x ON x.[ResponseId] = r.[Id];");

            // ترقيم الردود الموجودة RSP-yy-nnnn بحسب سنة الإنشاء ثم تحديث العدّاد كي لا يتكرر رقم
            migrationBuilder.Sql(@"
                ;WITH n AS (
                    SELECT [Id], YEAR([CreatedAt]) AS [Y], ROW_NUMBER() OVER (PARTITION BY YEAR([CreatedAt]) ORDER BY [Id]) AS [Rn]
                    FROM [StudyAssignmentResponses])
                UPDATE r SET r.[ResponseNumber] = 'RSP-' + RIGHT('0' + CAST(n.[Y] % 100 AS varchar(2)), 2) + '-' + RIGHT('0000' + CAST(n.[Rn] AS varchar(10)), 4)
                FROM [StudyAssignmentResponses] r JOIN n ON n.[Id] = r.[Id];");
            migrationBuilder.Sql(@"
                INSERT INTO [StudyNumberSequences] ([DocumentType], [Year], [LastNumber])
                SELECT 5, YEAR([CreatedAt]), COUNT(*) FROM [StudyAssignmentResponses] GROUP BY YEAR([CreatedAt]);");

            migrationBuilder.DropColumn(
                name: "InitialCost",
                table: "ResponseBuildings");

            migrationBuilder.DropColumn(
                name: "SiteArea",
                table: "ResponseBuildings");

            migrationBuilder.CreateIndex(
                name: "IX_StudyAssignmentResponses_ResponseNumber",
                table: "StudyAssignmentResponses",
                column: "ResponseNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudyAssignmentResponses_ResponseNumber",
                table: "StudyAssignmentResponses");

            migrationBuilder.AddColumn<decimal>(
                name: "InitialCost",
                table: "ResponseBuildings",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SiteArea",
                table: "ResponseBuildings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            // القيمتان تعودان إلى أول سطر مبنى لكل رد
            migrationBuilder.Sql(@"
                UPDATE b SET b.[InitialCost] = r.[InitialCost], b.[SiteArea] = r.[SiteArea]
                FROM [ResponseBuildings] b JOIN [StudyAssignmentResponses] r ON r.[Id] = b.[ResponseId]
                WHERE b.[BuildingType] = (SELECT MIN(b2.[BuildingType]) FROM [ResponseBuildings] b2 WHERE b2.[ResponseId] = b.[ResponseId]);");
            migrationBuilder.Sql("DELETE FROM [StudyNumberSequences] WHERE [DocumentType] = 5;");

            migrationBuilder.DropColumn(
                name: "DiwanRequestNumber",
                table: "StudyRequests");

            migrationBuilder.DropColumn(
                name: "InitialCost",
                table: "StudyAssignmentResponses");

            migrationBuilder.DropColumn(
                name: "ResponseNumber",
                table: "StudyAssignmentResponses");

            migrationBuilder.DropColumn(
                name: "SiteArea",
                table: "StudyAssignmentResponses");
        }
    }
}
