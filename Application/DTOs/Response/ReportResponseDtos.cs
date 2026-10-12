namespace Application.DTOs.Response
{
    /// <summary>أنواع أعمدة التقرير — تحدد التنسيق في Excel والطباعة والمعاينة</summary>
    public static class ReportColumnType
    {
        public const string Text = "text";
        public const string Number = "number";     // عدد صحيح
        public const string Decimal = "decimal";   // كمية/مدة بكسور
        public const string Money = "money";       // ل.س
        public const string Date = "date";
        public const string DateTime = "datetime";
    }

    /// <summary>أنواع فلاتر التقرير</summary>
    public static class ReportFilterType
    {
        public const string Date = "date";       // yyyy-MM-dd
        public const string Select = "select";   // قيمة من Options (الخيارات ضمن حدود المستخدم)
        public const string Text = "text";
        public const string Number = "number";   // عدد صحيح موجب
    }

    public class ReportColumnDto
    {
        public string Key { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public string Type { get; set; } = ReportColumnType.Text;
        /// <summary>يُطبع في نسخة A4 (الأعمدة الثانوية في Excel فقط كي يتّسع الجدول للورقة)</summary>
        public bool Print { get; set; } = true;
        /// <summary>يُجمع في سطر «المجموع» أسفل الجدول</summary>
        public bool Sum { get; set; }
    }

    public class ReportTableDto
    {
        public List<ReportColumnDto> Columns { get; set; } = [];
        /// <summary>كل صف قيم بترتيب الأعمدة (نص، رقم، تاريخ، أو null)</summary>
        public List<object?[]> Rows { get; set; } = [];
        /// <summary>عدد الصفوف المطابقة كلها (قد يزيد على المرسَل في المعاينة)</summary>
        public int Total { get; set; }
    }

    public record ReportOptionDto(string Value, string Label, string? Parent = null);

    public class ReportFilterDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Type { get; set; } = ReportFilterType.Select;
        public bool Required { get; set; }
        /// <summary>فلتر أب: تظهر من الخيارات ما يطابق Parent قيمته المختارة (القسم حسب الفرع مثلاً)</summary>
        public string? DependsOn { get; set; }
        public string? Default { get; set; }
        public string? Hint { get; set; }
        public List<ReportOptionDto> Options { get; set; } = [];
    }

    public class ReportInfoDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Landscape { get; set; }
    }

    public class ReportDefinitionDto : ReportInfoDto
    {
        public List<ReportFilterDto> Filters { get; set; } = [];
        public int PreviewRows { get; set; }
        public int MaxExcelRows { get; set; }
        public int MaxPrintRows { get; set; }
    }

    public class ReportPreviewDto
    {
        public string FiltersText { get; set; } = string.Empty;
        public ReportTableDto Table { get; set; } = new();
    }

    /// <summary>بيانات صفحة الطباعة A4: الترويسة (فرع الطابع) + الجدول كاملاً</summary>
    public class ReportPrintDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Landscape { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string FiltersText { get; set; } = string.Empty;
        public string PrintedBy { get; set; } = string.Empty;
        public DateTime PrintedAt { get; set; }
        public ReportTableDto Table { get; set; } = new();
    }

    public class ReportFileDto
    {
        public byte[] Content { get; set; } = [];
        public string FileName { get; set; } = string.Empty;
    }

    public class ReportLogDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string ReportKey { get; set; } = string.Empty;
        public string ReportTitle { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;   // Excel | طباعة
        public string Filters { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
