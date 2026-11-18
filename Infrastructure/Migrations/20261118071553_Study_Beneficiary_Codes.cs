using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Beneficiary_Codes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "StudyBeneficiaries",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            // استعادة الرمز الموجود فقط عندما يكون واحداً للجهة وغير مستخدم لجهة أخرى.
            // تبقى الحالات الناقصة أو المتعارضة بلا رمز ليستكملها المسؤول من الإعدادات.
            migrationBuilder.Sql("""
                WITH ExistingCodes AS (
                    SELECT DISTINCT [MainBeneficiaryId], LTRIM(RTRIM([EntityCode])) AS [Code]
                    FROM [StudyRequests]
                    WHERE [MainBeneficiaryId] IS NOT NULL
                      AND LTRIM(RTRIM([EntityCode])) <> N''
                ), SingleCodes AS (
                    SELECT [MainBeneficiaryId], MIN([Code]) AS [Code]
                    FROM ExistingCodes
                    GROUP BY [MainBeneficiaryId]
                    HAVING COUNT(*) = 1
                ), UniqueCodes AS (
                    SELECT [Code]
                    FROM ExistingCodes
                    GROUP BY [Code]
                    HAVING COUNT(*) = 1
                )
                UPDATE b SET [Code] = c.[Code]
                FROM [StudyBeneficiaries] b
                INNER JOIN SingleCodes c ON c.[MainBeneficiaryId] = b.[Id]
                INNER JOIN UniqueCodes u ON u.[Code] = c.[Code];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StudyBeneficiaries_Code",
                table: "StudyBeneficiaries",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudyBeneficiaries_Code",
                table: "StudyBeneficiaries",
                sql: "[Code] IS NULL OR LEN(LTRIM(RTRIM([Code]))) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudyBeneficiaries_Code",
                table: "StudyBeneficiaries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudyBeneficiaries_Code",
                table: "StudyBeneficiaries");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "StudyBeneficiaries");
        }
    }
}
