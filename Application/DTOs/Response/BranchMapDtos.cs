namespace Application.DTOs.Response
{
    /// <summary>
    /// خريطة فرع (2026-10-11): طبقات تُبنى من بيانات الفرع نفسه، بلا أي إعداد يربط فرعاً بخريطة.
    /// الطبقة الأولى «تركيبات الأجهزة» = المواقع التي فيها تركيبات مسجّلة باسم الفرع. الإصدار 1.2.0 يضيف
    /// طبقات يعرّفها كل فرع بحقوله (Kind = "entries") دون تغيير هذا الشكل.
    /// </summary>
    public class BranchMapDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;

        /// <summary>فارغة = لا توجد بيانات خريطة لهذا الفرع بعد</summary>
        public List<MapLayerDto> Layers { get; set; } = [];
    }

    /// <summary>قائمة اختيار الفرع في الخريطة</summary>
    public class MapBranchOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool HasMapData { get; set; }
    }

    public class MapLayerDto
    {
        /// <summary>معرّف ثابت للطبقة داخل الخريطة (مثل "devices")</summary>
        public string Key { get; set; } = string.Empty;
        /// <summary>"devices" = تركيبات الأجهزة، "entries" = طبقة يعرّفها الفرع (سجلات على المواقع)</summary>
        public string Kind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>#RRGGBB — فارغ = لون الواجهة الافتراضي</summary>
        public string Color { get; set; } = string.Empty;
        public List<MapSiteDto> Sites { get; set; } = [];
        public int SitesWithoutCoordinates { get; set; }
    }

    public class MapSiteDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GovernorateCode { get; set; } = string.Empty;
        public string GovernorateName { get; set; } = string.Empty;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int InstallationsCount { get; set; }
        public int DeviceTypesCount { get; set; }   // عدد أنواع الأجهزة المختلفة في الموقع
        /// <summary>عدد سجلات الطبقة على الموقع (طبقات الفروع)</summary>
        public int EntriesCount { get; set; }
    }
}
