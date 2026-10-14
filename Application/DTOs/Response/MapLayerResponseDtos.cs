using System.Text.Json;

namespace Application.DTOs.Response
{
    public class MapLayerFieldDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public int Type { get; set; }
        public bool IsRequired { get; set; }
        public List<string> Options { get; set; } = [];
        public int SortOrder { get; set; }
        /// <summary>مخفي: لا يظهر في النماذج، وقيمه محفوظة (يُعاد من صفحة الطبقات)</summary>
        public bool IsHidden { get; set; }
        /// <summary>حقل الملفات (7): أقصى عدد ملفات، وأقصى حجم للملف بالـ MB، ومجموعات الصيغ</summary>
        public int MaxFiles { get; set; }
        public int MaxFileMegabytes { get; set; }
        public List<string> FileKinds { get; set; } = [];
    }

    public class MapLayerDefinitionDto
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public bool AllowMultiple { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        /// <summary>فارغ = إدخال يدوي؛ وإلا مفتاح مزوّد من بيانات النظام</summary>
        public string Source { get; set; } = string.Empty;
        public string SourceTitle { get; set; } = string.Empty;
        public Dictionary<string, List<string>> SourceFilter { get; set; } = new();
        public int EntriesCount { get; set; }
        public List<MapLayerFieldDto> Fields { get; set; } = [];
        public byte[] RowVersion { get; set; } = [];
    }

    public class MapEntryDto
    {
        public int Id { get; set; }
        public int LayerId { get; set; }
        public string LayerName { get; set; } = string.Empty;
        public string LayerColor { get; set; } = string.Empty;
        public int SiteId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        /// <summary>القيم كما حُفظت: {"&lt;fieldId&gt;": قيمة}</summary>
        public Dictionary<string, JsonElement> Values { get; set; } = new();
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string UpdatedByName { get; set; } = string.Empty;
        public DateTime? UpdatedAt { get; set; }
        /// <summary>ملفات حقول «ملفات» (كل ملف يحمل FieldId)</summary>
        public List<MapEntryFileDto> Files { get; set; } = [];
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>مزوّد من بيانات النظام يمكن أن تُبنى منه طبقة، بفلاتره من بيانات الفرع</summary>
    public class MapSourceDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public List<MapSourceFilterDto> Filters { get; set; } = [];
    }

    public class MapSourceFilterDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<MapSourceOptionDto> Options { get; set; } = [];
    }

    public record MapSourceOptionDto(string Value, string Label);

    public class MapEntryFileDto
    {
        public int Id { get; set; }
        public int FieldId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        /// <summary>مجموعة الصيغة كما كشفها الخادم (images, pdf, office…)</summary>
        public string Kind { get; set; } = string.Empty;
        public long Size { get; set; }
        /// <summary>صورة أو PDF تُعاين داخل الصفحة؛ غيرها يُنزَّل</summary>
        public bool Previewable { get; set; }
        public string UploadedByName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    /// <summary>محتوى ملف للتنزيل أو المعاينة</summary>
    public record MapEntryFileContentDto(string FileName, string ContentType, Stream Content, bool Previewable);

    /// <summary>إعدادات رفع الملفات: سقف النظام ومجموعات الصيغ (لمنشئ الحقول ونموذج السجل)</summary>
    public class MapFileOptionsDto
    {
        public int MaxFileMegabytes { get; set; }
        public int MaxFilesPerField { get; set; }
        public List<MapFileKindDto> Kinds { get; set; } = [];
    }

    public class MapFileKindDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Formats { get; set; } = string.Empty;
        public List<string> Extensions { get; set; } = [];
    }

    /// <summary>
    /// معلومات الفروع عن موقع: السجلات التي أراها، والطبقات الفعّالة التي أستطيع الإضافة إليها (مع حقولها الظاهرة)،
    /// وطبقات السجلات المعروضة وإن كانت معطّلة (IsActive=false: للقراءة فقط).
    /// </summary>
    public class SiteEntriesDto
    {
        public List<MapLayerDefinitionDto> Layers { get; set; } = [];
        public List<MapEntryDto> Entries { get; set; } = [];
        public bool CanCreate { get; set; }
    }

    public class FieldUsageDto
    {
        public int Count { get; set; }
    }
}
