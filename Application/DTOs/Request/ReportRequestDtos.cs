namespace Application.DTOs.Request
{
    /// <summary>قيم فلاتر التقرير: مفتاح الفلتر ← قيمته (نص؛ التاريخ yyyy-MM-dd)</summary>
    public class ReportRunRequestDto
    {
        public Dictionary<string, string?> Filters { get; set; } = [];
    }

    public class ReportLogFilterDto
    {
        public string? ReportKey { get; set; }
        public string? Search { get; set; }   // اسم المستخدم
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
