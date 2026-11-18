using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Study_Beneficiaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MainBeneficiaryId",
                table: "StudyRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StudyBeneficiaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyBeneficiaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudyRequests_MainBeneficiaryId",
                table: "StudyRequests",
                column: "MainBeneficiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyBeneficiaries_Name",
                table: "StudyBeneficiaries",
                column: "Name",
                unique: true);

            // استيراد الجهات الموجودة وربط الطلبات بها، مع إبقاء نص المستند الأصلي كما هو.
            migrationBuilder.Sql("""
                INSERT INTO [StudyBeneficiaries] ([Name], [IsActive])
                SELECT DISTINCT LTRIM(RTRIM([MainBeneficiary])), CAST(1 AS bit)
                FROM [StudyRequests]
                WHERE LTRIM(RTRIM([MainBeneficiary])) <> N'';

                UPDATE r SET [MainBeneficiaryId] = b.[Id]
                FROM [StudyRequests] r
                INNER JOIN [StudyBeneficiaries] b ON b.[Name] = LTRIM(RTRIM(r.[MainBeneficiary]));
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyRequests_StudyBeneficiaries_MainBeneficiaryId",
                table: "StudyRequests",
                column: "MainBeneficiaryId",
                principalTable: "StudyBeneficiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudyRequests_StudyBeneficiaries_MainBeneficiaryId",
                table: "StudyRequests");

            migrationBuilder.DropTable(
                name: "StudyBeneficiaries");

            migrationBuilder.DropIndex(
                name: "IX_StudyRequests_MainBeneficiaryId",
                table: "StudyRequests");

            migrationBuilder.DropColumn(
                name: "MainBeneficiaryId",
                table: "StudyRequests");
        }
    }
}
