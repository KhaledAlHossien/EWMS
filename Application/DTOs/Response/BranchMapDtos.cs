namespace Application.DTOs.Response
{
    /// <summary>
    /// خريطة فرع: طبقات بيانات حسب وظيفة الفرع. حالياً طبقة واحدة (مواقع توثيق الأجهزة ومحافظاتها)
    /// تخص فرع قسم العمليات؛ لاحقاً يُضاف لكل فرع طبقته دون تغيير شكل الاستجابة الأساسي.
    /// </summary>
    public class BranchMapDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;

        /// <summary>null = لا توجد بيانات خريطة لهذا الفرع بعد</summary>
        public DevicesMapLayerDto? DevicesLayer { get; set; }
    }

    /// <summary>قائمة اختيار الفرع في الخريطة</summary>
    public class MapBranchOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool HasMapData { get; set; }
    }

    public class DevicesMapLayerDto
    {
        public string Title { get; set; } = "المواقع";
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
    }
}
