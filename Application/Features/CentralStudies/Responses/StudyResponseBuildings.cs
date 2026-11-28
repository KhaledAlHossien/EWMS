using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;

namespace Application.Features.CentralStudies.Responses
{
    /// <summary>
    /// الشكل الموحّد لمباني إشعار الرد وبنود تكلفته (طلب الواجهة 2026-11-26) — المصدر الوحيد للقاعدة، يستعمله المدقق وApply:
    /// لكل نوع مبنى سطر تفاصيل لكل مبنى (العدد = عدد الأسطر)، ولكل سطر عدد تكرارات (2026-11-28): المساحة كاملة للنوع
    /// = مجموع (المساحة × التكرار) يحسبها الخادم. وبند تكلفة واحد لكل سطر بالترتيب نفسه (النوع ثم ترتيب الأسطر):
    /// الوصف «النوع — الرمز — الوظيفة»، الوحدة م²، الكمية = مساحة المبنى الواحد، والتكرار = تكرار المبنى،
    /// فالكلفة كاملة = الكمية × سعر الوحدة × التكرار.
    /// الردود القديمة حُوّلت إلى هذا الشكل بالترحيل Study_Response_Unified_Buildings (وتكرارها 1).
    /// </summary>
    public static class StudyResponseBuildings
    {
        public const string Unit = "م²";
        public const string Separator = " — ";

        public static string Description(StudyBuildingType type, string code, string function) =>
            StudyRules.Name(type) + Separator + code.Trim() + Separator + function.Trim();

        /// <summary>رسالة عربية بأول مخالفة للشكل الموحّد، أو null</summary>
        public static string? Problem(StudyResponseFormDto dto)
        {
            foreach (var b in dto.Buildings)
            {
                if (b.BuildingCount < 1)
                    return "عدد المباني لكل نوع واحد على الأقل (احذف النوع الذي لا مباني له)";
                if (b.Details == null || b.Details.Count != b.BuildingCount)
                    return "يجب أن يطابق عدد صفوف التفاصيل عدد المباني: أدخل الرمز والوظيفة والمساحة لكل مبنى";
            }

            var expected = dto.Buildings.OrderBy(b => b.BuildingType)
                .SelectMany(b => b.Details.Select(d => (Type: (StudyBuildingType)b.BuildingType, Detail: d)))
                .ToList();
            if (dto.CostItems.Count != expected.Count)
                return "بنود التكلفة بند واحد لكل مبنى من تفاصيل المباني";
            for (var i = 0; i < expected.Count; i++)
            {
                var (type, d) = expected[i];
                var c = dto.CostItems[i];
                if (c.Unit.Trim() != Unit || c.Quantity != d.Area || c.Repetitions != d.Repetitions ||
                    c.Description.Trim() != Description(type, d.Code, d.Function))
                    return $"بند التكلفة {i + 1} لا يطابق مبناه: الوصف «النوع — الرمز — الوظيفة»، والوحدة م²، والكمية مساحة المبنى الواحد، والتكرار تكرار المبنى";
            }
            return null;
        }
    }
}
