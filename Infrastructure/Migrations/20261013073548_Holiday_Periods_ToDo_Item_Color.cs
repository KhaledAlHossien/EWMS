using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Holiday_Periods_ToDo_Item_Color : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Date",
                table: "PublicHolidays",
                newName: "StartDate");

            migrationBuilder.RenameIndex(
                name: "IX_PublicHolidays_Date",
                table: "PublicHolidays",
                newName: "IX_PublicHolidays_StartDate");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "ToDoItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "PublicHolidays",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // العطلة سجلٌّ واحد بفترة (قرار المستخدم 2026-10-13): الأيام المتتالية بالاسم نفسه تُدمج في سجل أول يوم منها
            // (جزر التواريخ: التاريخ ناقص ترتيبه ثابتٌ داخل كل سلسلة متتالية)، وتُحذف بقية أيامها. يومٌ ناقص بينها يفصل عطلتين.
            migrationBuilder.Sql(@"
UPDATE PublicHolidays SET EndDate = StartDate;

;WITH d AS (
    SELECT Id, StartDate, Name,
           DATEADD(day, -CAST(ROW_NUMBER() OVER (PARTITION BY Name ORDER BY StartDate) AS int), StartDate) AS Grp
    FROM PublicHolidays
), g AS (
    SELECT Name, Grp, MIN(StartDate) AS S, MAX(StartDate) AS E FROM d GROUP BY Name, Grp
)
UPDATE h SET EndDate = g.E
FROM PublicHolidays h
INNER JOIN d ON d.Id = h.Id
INNER JOIN g ON g.Name = d.Name AND g.Grp = d.Grp
WHERE h.StartDate = g.S;

DELETE h FROM PublicHolidays h
WHERE EXISTS (SELECT 1 FROM PublicHolidays k
              WHERE k.Id <> h.Id AND k.Name = h.Name AND h.StartDate > k.StartDate AND h.StartDate <= k.EndDate);");

            migrationBuilder.CreateIndex(
                name: "IX_PublicHolidays_EndDate_StartDate",
                table: "PublicHolidays",
                columns: new[] { "EndDate", "StartDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // رجوع: كل عطلة تعود يوماً لكل سجل (العطلة الواحدة لا تتجاوز 31 يوماً)
            migrationBuilder.Sql(@"
;WITH n AS (SELECT TOP (40) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int) AS i FROM sys.all_objects)
INSERT INTO PublicHolidays (StartDate, EndDate, Name)
SELECT DATEADD(day, n.i, h.StartDate), DATEADD(day, n.i, h.StartDate), h.Name
FROM PublicHolidays h INNER JOIN n ON DATEADD(day, n.i, h.StartDate) <= h.EndDate;");

            migrationBuilder.DropIndex(
                name: "IX_PublicHolidays_EndDate_StartDate",
                table: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "ToDoItems");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "PublicHolidays");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "PublicHolidays",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "IX_PublicHolidays_StartDate",
                table: "PublicHolidays",
                newName: "IX_PublicHolidays_Date");
        }
    }
}
