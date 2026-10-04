using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Vacation_Working_Days_And_Payment_At_Approval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalApprovedAt",
                table: "Vacation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinalApprovedByUserId",
                table: "Vacation",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstApprovedAt",
                table: "Vacation",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaidDays",
                table: "Vacation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Vacation",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "UnpaidDays",
                table: "Vacation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VacationSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VacationId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacationSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VacationSegments_Vacation_VacationId",
                        column: x => x.VacationId,
                        principalTable: "Vacation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vacation_FinalApprovedByUserId",
                table: "Vacation",
                column: "FinalApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_Date",
                table: "PublicHolidays",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VacationSegments_VacationId_StartDate",
                table: "VacationSegments",
                columns: new[] { "VacationId", "StartDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Vacation_Users_FinalApprovedByUserId",
                table: "Vacation",
                column: "FinalApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // الطلبات المعلّقة (قرار المستخدم 2026-10-04): الدفع يُحدَّد عند الاعتماد فلا يُحجز لها رصيد الآن،
            // والمدة تُحسب بأيام العمل (بلا جمعة؛ لا عطل رسمية مسجّلة بعد). 1900-01-05 كان يوم جمعة.
            // الإجازات المعتمدة القديمة تبقى كما هي، وتُنشأ أجزاؤها عند التشغيل (DbSeeder.BackfillVacationSegmentsAsync).
            migrationBuilder.Sql(@"
UPDATE v SET
    IsPaid = 0,
    VacDayCount = (
        SELECT COUNT(*)
        FROM (SELECT TOP (DATEDIFF(day, v.StartVac, v.EndVac) + 1)
                     ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
              FROM sys.all_objects a CROSS JOIN sys.all_objects b) d
        WHERE DATEDIFF(day, '19000105', DATEADD(day, d.n, CAST(v.StartVac AS date))) % 7 <> 0)
FROM Vacation v
WHERE v.Status IN (1, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vacation_Users_FinalApprovedByUserId",
                table: "Vacation");

            migrationBuilder.DropTable(
                name: "PublicHolidays");

            migrationBuilder.DropTable(
                name: "VacationSegments");

            migrationBuilder.DropIndex(
                name: "IX_Vacation_FinalApprovedByUserId",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "FinalApprovedAt",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "FinalApprovedByUserId",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "FirstApprovedAt",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "PaidDays",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Vacation");

            migrationBuilder.DropColumn(
                name: "UnpaidDays",
                table: "Vacation");
        }
    }
}
