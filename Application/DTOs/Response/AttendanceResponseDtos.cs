namespace Application.DTOs.Response
{
    public class IdNameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class AttendanceAreaDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<IdNameDto> Branches { get; set; } = [];
        /// <summary>الأجهزة المقبولة في المنطقة، والمتصل منها الآن</summary>
        public int Devices { get; set; }
        public int OnlineDevices { get; set; }
        /// <summary>الأشخاص الفعّالون على أجهزة المنطقة (لهم رقم بصمة)</summary>
        public int People { get; set; }
        public byte[] RowVersion { get; set; } = [];
    }

    /// <summary>فرع مع منطقته الحالية (لاختيار فروع المنطقة)</summary>
    public class AreaBranchOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
    }
}
