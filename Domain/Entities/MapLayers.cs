namespace Domain.Entities
{
    /// <summary>نوع حقل في طبقة خريطة (يحدد التحقق وطريقة الإدخال والعرض)</summary>
    public enum MapFieldType
    {
        Text = 1,       // نص قصير (حتى 500 حرف)
        LongText = 2,   // نص طويل (حتى 4000 حرف)
        Number = 3,
        Date = 4,       // يوم فقط yyyy-MM-dd
        Select = 5,     // قائمة اختيار من Options
        YesNo = 6
    }

    /// <summary>
    /// طبقة خريطة يعرّفها فرع (الإصدار 1.2.0، قرار المستخدم 2026-10-11): نوع معلومات يسجّله الفرع على المواقع،
    /// بحقول تُعرَّف من الواجهة بلا برمجة. الطبقة ملك الفرع ويستعملها كل أقسامه؛ كل سجل يحمل قسم من أدخله.
    /// </summary>
    public class MapLayer
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        public string Name { get; set; } = string.Empty;          // فريد داخل الفرع
        public string Description { get; set; } = string.Empty;
        /// <summary>لون نقاط الطبقة على الخريطة #RRGGBB</summary>
        public string Color { get; set; } = "#2563EB";
        /// <summary>عدة سجلات للموقع الواحد (مثل «الموظفون» أو «المشاريع») أم سجل واحد (مثل «بيانات القطعة»)</summary>
        public bool AllowMultiple { get; set; }
        public int SortOrder { get; set; }
        /// <summary>المعطّلة لا تظهر على الخريطة ولا في نماذج الإضافة، وتبقى سجلاتها</summary>
        public bool IsActive { get; set; } = true;

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<MapLayerField> Fields { get; set; } = new List<MapLayerField>();
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>حقل في طبقة. الحذف مع وجود قيم يُخيَّر فيه المستخدم: إخفاء (تبقى القيم) أو حذف نهائي مع قيمه</summary>
    public class MapLayerField
    {
        public int Id { get; set; }
        public int LayerId { get; set; }
        public MapLayer Layer { get; set; } = null!;

        public string Label { get; set; } = string.Empty;
        public MapFieldType Type { get; set; } = MapFieldType.Text;
        public bool IsRequired { get; set; }
        /// <summary>خيارات القائمة (Select) — مصفوفة JSON من النصوص</summary>
        public string Options { get; set; } = "[]";
        public int SortOrder { get; set; }
        /// <summary>مخفي: لا يظهر في النماذج والعرض، وقيمه محفوظة ويمكن إعادته</summary>
        public bool IsHidden { get; set; }
    }

    /// <summary>
    /// سجل طبقة على موقع: معلومات الفرع عن الموقع. يحمل قسم وفرع من أدخله (الفرع = فرع الطبقة)،
    /// وهما حدّ العرض والإدارة (قسمي / فرعي). القيم JSON مفاتيحها معرّفات الحقول، فإعادة تسمية الحقل لا تمسّها.
    /// </summary>
    public class MapEntry
    {
        public int Id { get; set; }
        public int LayerId { get; set; }
        public MapLayer Layer { get; set; } = null!;
        public int SiteId { get; set; }
        public Site Site { get; set; } = null!;

        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        /// <summary>{"&lt;fieldId&gt;": قيمة} — يتحقق منها الخادم حسب حقول الطبقة</summary>
        public string Values { get; set; } = "{}";

        public int CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int? UpdatedByUserId { get; set; }
        public User? UpdatedByUser { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public byte[] RowVersion { get; set; } = [];
    }
}
