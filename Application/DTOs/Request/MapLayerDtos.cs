using System.Text.Json;

namespace Application.DTOs.Request
{
    /// <summary>حقل في نموذج الطبقة: Id = حقل موجود يُعدَّل، وبدونه حقل جديد. الترتيب = ترتيب القائمة</summary>
    public class MapLayerFieldRequestDto
    {
        public int? Id { get; set; }
        public string Label { get; set; } = string.Empty;
        /// <summary>1 نص، 2 نص طويل، 3 رقم، 4 تاريخ، 5 قائمة اختيار، 6 نعم/لا</summary>
        public int Type { get; set; } = 1;
        public bool IsRequired { get; set; }
        /// <summary>خيارات القائمة (للنوع 5 فقط)</summary>
        public List<string> Options { get; set; } = [];
    }

    public class MapLayerRequestDto
    {
        /// <summary>الفرع (لمن يملك نظرة المؤسسة فقط؛ لغيره فرعه دائماً). لا يتغير بعد الإنشاء</summary>
        public int? BranchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = "#2563EB";
        public bool AllowMultiple { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>الحقول المرسلة تُضاف أو تُعدَّل؛ الحذف من DELETE Field/{id} (يخيّر بين الإخفاء والحذف النهائي)</summary>
        public List<MapLayerFieldRequestDto> Fields { get; set; } = [];
        public byte[]? RowVersion { get; set; }
    }

    public class MapEntryRequestDto
    {
        public int LayerId { get; set; }
        public int SiteId { get; set; }
        /// <summary>{"&lt;fieldId&gt;": قيمة} — نص، رقم، تاريخ yyyy-MM-dd، خيار من القائمة، أو true/false</summary>
        public Dictionary<string, JsonElement> Values { get; set; } = new();
        /// <summary>القسم المالك (لمن يملك نظرة المؤسسة أو ViewBranchMapEntries، ضمن فرع الطبقة) — فارغ = قسم من يضيف</summary>
        public int? DepartmentId { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
