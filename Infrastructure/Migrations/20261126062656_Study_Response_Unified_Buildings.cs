using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// تحويل ردود الدراسات القديمة إلى الشكل الموحّد (طلب الواجهة 2026-11-26، سكربتها المجرَّب مع ثلاث إضافات):
    /// لكل مبنى سطر تفاصيل، ولكل سطر بند تكلفة «النوع — الرمز — الوظيفة» بوحدة م² وكمية = مساحته، بسعر متر يحفظ إجمالي
    /// الرد القديم (فرق تقريب سعر المتر فقط). المبنى بلا تفاصيل يصير أسطراً رموزها M1 M2… ووظيفتها «غير محددة».
    /// الإضافات: (1) نسخة من أسطر كل رد يُحوَّل قبل تغييره في الجداول StudyResponseLegacy* — Down يعيدها، وتُحذف بترحيل
    /// لاحق بعد التحقق؛ (2) رد له بنود تكلفة بلا مبانٍ تبقى بنوده كما هي (لا يُصفَّر إجماليه)، ويكمله صاحبه عند التعديل؛
    /// (3) مساحة السطر لا تقل عن 0.01 (القيد CK_ResponseBuildingDetails_Area). الردود الموحّدة أصلاً لا تُمس.
    /// </summary>
    public partial class Study_Response_Unified_Buildings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @Label TABLE (BuildingType int PRIMARY KEY, Label nvarchar(50));
                INSERT @Label VALUES (1, N'إعادة تأهيل'), (2, N'إنشاء جديد');
                DECLARE @Sep nvarchar(5) = N' ' + NCHAR(8212) + N' ';
                DECLARE @M2 nvarchar(5) = N'م' + NCHAR(178);

                -- 1) الردود الموحّدة أصلاً لا تُمس
                DECLARE @Unified TABLE (ResponseId int PRIMARY KEY);
                WITH D AS (SELECT d.ResponseId, d.Area, l.Label + @Sep + LTRIM(RTRIM(d.Code)) + @Sep + LTRIM(RTRIM(d.[Function])) AS Expected,
                                  ROW_NUMBER() OVER (PARTITION BY d.ResponseId ORDER BY d.BuildingType, d.SortOrder) AS n
                           FROM ResponseBuildingDetails d JOIN @Label l ON l.BuildingType = d.BuildingType),
                     C AS (SELECT c.ResponseId, c.Description, c.Unit, c.Quantity, ROW_NUMBER() OVER (PARTITION BY c.ResponseId ORDER BY c.SortOrder) AS n
                           FROM ResponseCostItems c)
                INSERT @Unified SELECT r.Id FROM StudyAssignmentResponses r
                WHERE EXISTS (SELECT 1 FROM ResponseBuildingDetails d WHERE d.ResponseId = r.Id)
                  AND NOT EXISTS (SELECT 1 FROM ResponseBuildings b WHERE b.ResponseId = r.Id AND b.BuildingCount <>
                      (SELECT COUNT(*) FROM ResponseBuildingDetails d WHERE d.ResponseId = b.ResponseId AND d.BuildingType = b.BuildingType))
                  AND (SELECT COUNT(*) FROM C WHERE C.ResponseId = r.Id) = (SELECT COUNT(*) FROM D WHERE D.ResponseId = r.Id)
                  AND NOT EXISTS (SELECT 1 FROM D JOIN C ON C.ResponseId = D.ResponseId AND C.n = D.n
                      WHERE D.ResponseId = r.Id AND (C.Unit <> @M2 OR C.Quantity <> D.Area OR C.Description <> D.Expected));

                DECLARE @Legacy TABLE (ResponseId int PRIMARY KEY, OldTotal decimal(28,4));
                INSERT @Legacy SELECT r.Id, ISNULL((SELECT SUM(c.Quantity * c.UnitPrice) FROM ResponseCostItems c WHERE c.ResponseId = r.Id), 0)
                FROM StudyAssignmentResponses r
                WHERE r.Id NOT IN (SELECT ResponseId FROM @Unified)
                  AND (EXISTS (SELECT 1 FROM ResponseBuildings b WHERE b.ResponseId = r.Id) OR EXISTS (SELECT 1 FROM ResponseCostItems c WHERE c.ResponseId = r.Id));

                -- نسخة احتياطية من أسطر الردود التي ستُحوَّل (يعيدها Down)
                SELECT b.* INTO StudyResponseLegacyBuildings FROM ResponseBuildings b JOIN @Legacy g ON g.ResponseId = b.ResponseId;
                SELECT d.* INTO StudyResponseLegacyBuildingDetails FROM ResponseBuildingDetails d JOIN @Legacy g ON g.ResponseId = d.ResponseId;
                SELECT c.* INTO StudyResponseLegacyCostItems FROM ResponseCostItems c JOIN @Legacy g ON g.ResponseId = c.ResponseId;

                -- 2) أسطر تفاصيل للمباني بلا تفاصيل (المساحة بالتساوي، الباقي على الأخير، ولا تقل عن 0.01)
                WITH N AS (SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i FROM sys.all_objects),
                     B AS (SELECT b.ResponseId, b.BuildingType, b.BuildingCount AS cnt, b.BuildingsArea AS total, ROUND(b.BuildingsArea / b.BuildingCount, 2, 1) AS each
                           FROM ResponseBuildings b JOIN @Legacy g ON g.ResponseId = b.ResponseId
                           WHERE b.BuildingCount > 0 AND NOT EXISTS (SELECT 1 FROM ResponseBuildingDetails d WHERE d.ResponseId = b.ResponseId AND d.BuildingType = b.BuildingType))
                INSERT ResponseBuildingDetails (ResponseId, BuildingType, SortOrder, Code, [Function], Area)
                SELECT B.ResponseId, B.BuildingType, N.i, N'M' + CAST(N.i AS nvarchar(10)), N'غير محددة',
                       CASE WHEN v.area < 0.01 THEN 0.01 ELSE v.area END
                FROM B JOIN N ON N.i <= B.cnt
                CROSS APPLY (SELECT CASE WHEN N.i = B.cnt THEN B.total - B.each * (B.cnt - 1) ELSE B.each END AS area) v;

                -- 3) الأنواع الفارغة تُحذف، والعدد والمساحة من التفاصيل
                DELETE b FROM ResponseBuildings b JOIN @Legacy g ON g.ResponseId = b.ResponseId
                WHERE NOT EXISTS (SELECT 1 FROM ResponseBuildingDetails d WHERE d.ResponseId = b.ResponseId AND d.BuildingType = b.BuildingType);
                UPDATE b SET
                  BuildingCount = (SELECT COUNT(*) FROM ResponseBuildingDetails d WHERE d.ResponseId = b.ResponseId AND d.BuildingType = b.BuildingType),
                  BuildingsArea = (SELECT SUM(d.Area) FROM ResponseBuildingDetails d WHERE d.ResponseId = b.ResponseId AND d.BuildingType = b.BuildingType)
                FROM ResponseBuildings b JOIN @Legacy g ON g.ResponseId = b.ResponseId;

                -- 4) بند لكل مبنى بسعر متر يحفظ الإجمالي القديم (الرد بلا مبانٍ يحتفظ ببنوده)
                DECLARE @Price TABLE (ResponseId int PRIMARY KEY, UnitPrice decimal(18,2));
                INSERT @Price SELECT g.ResponseId, ROUND(g.OldTotal / a.Area, 2)
                FROM @Legacy g CROSS APPLY (SELECT ISNULL(SUM(d.Area), 0) AS Area FROM ResponseBuildingDetails d WHERE d.ResponseId = g.ResponseId) a
                WHERE a.Area > 0;
                DELETE c FROM ResponseCostItems c JOIN @Price p ON p.ResponseId = c.ResponseId;
                INSERT ResponseCostItems (ResponseId, SortOrder, Description, Unit, Quantity, UnitPrice)
                SELECT d.ResponseId, ROW_NUMBER() OVER (PARTITION BY d.ResponseId ORDER BY d.BuildingType, d.SortOrder),
                       l.Label + @Sep + LTRIM(RTRIM(d.Code)) + @Sep + LTRIM(RTRIM(d.[Function])), @M2, d.Area, p.UnitPrice
                FROM ResponseBuildingDetails d JOIN @Price p ON p.ResponseId = d.ResponseId JOIN @Label l ON l.BuildingType = d.BuildingType;
                """);
        }

        /// <summary>يعيد أسطر الردود المحوَّلة من النسخة الاحتياطية كما كانت، ويحذف جداولها</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'StudyResponseLegacyBuildings') IS NULL RETURN;
                DECLARE @Ids TABLE (ResponseId int PRIMARY KEY);
                INSERT @Ids SELECT ResponseId FROM StudyResponseLegacyBuildings
                      UNION SELECT ResponseId FROM StudyResponseLegacyBuildingDetails
                      UNION SELECT ResponseId FROM StudyResponseLegacyCostItems;
                DELETE c FROM ResponseCostItems c JOIN @Ids i ON i.ResponseId = c.ResponseId;
                DELETE d FROM ResponseBuildingDetails d JOIN @Ids i ON i.ResponseId = d.ResponseId;
                DELETE b FROM ResponseBuildings b JOIN @Ids i ON i.ResponseId = b.ResponseId;
                INSERT ResponseBuildings (ResponseId, BuildingType, BuildingCount, BuildingsArea)
                SELECT ResponseId, BuildingType, BuildingCount, BuildingsArea FROM StudyResponseLegacyBuildings;
                INSERT ResponseBuildingDetails (ResponseId, BuildingType, SortOrder, Code, [Function], Area)
                SELECT ResponseId, BuildingType, SortOrder, Code, [Function], Area FROM StudyResponseLegacyBuildingDetails;
                INSERT ResponseCostItems (ResponseId, SortOrder, Description, Unit, Quantity, UnitPrice)
                SELECT ResponseId, SortOrder, Description, Unit, Quantity, UnitPrice FROM StudyResponseLegacyCostItems;
                DROP TABLE StudyResponseLegacyCostItems;
                DROP TABLE StudyResponseLegacyBuildingDetails;
                DROP TABLE StudyResponseLegacyBuildings;
                """);
        }
    }
}
