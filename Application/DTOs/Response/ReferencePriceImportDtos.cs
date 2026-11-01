namespace Application.DTOs.Response
{
    /// <summary>سطر في تقرير استيراد الأسعار المرجعية: ما قُرئ من الملف وأخطاؤه (فارغة = صالح)</summary>
    public class ReferencePriceImportRowDto
    {
        public int Row { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public string UnitPrice { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string PriceDate { get; set; } = string.Empty;
        public string SourceDocument { get; set; } = string.Empty;
        public bool Valid { get; set; }
        public List<string> Errors { get; set; } = [];
    }

    /// <summary>
    /// تقرير الاستيراد: المعاينة (dryRun) أو الحفظ. الكل أو لا شيء: إن وُجد سطر خاطئ فلا يُحفظ أي سطر (Saved=false) ويُعاد التقرير بأخطائه.
    /// </summary>
    public class ReferencePriceImportReportDto
    {
        public bool DryRun { get; set; }
        /// <summary>هل حُفظت الأسعار فعلاً</summary>
        public bool Saved { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Valid { get; set; }
        public int Invalid { get; set; }
        public int Imported { get; set; }
        public int? ImportId { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ReferencePriceImportRowDto> Rows { get; set; } = [];
    }
}
