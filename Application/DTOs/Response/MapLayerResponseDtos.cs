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
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public byte[] RowVersion { get; set; } = [];
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
