using System.Globalization;
using Application.Common;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.Reports
{
    /// <summary>
    /// قواعد مركز التقارير (2026-10-12، قرارات المستخدم): صلاحية سحب لكل تقرير (حدّها حدّ صفحته)، Excel + طباعة A4،
    /// وسجل لكل سحب (المعاينة لا تُسجَّل). مصدر واحد للحدود والتحقق من الفلاتر ونص الفلاتر في الملف والسجل.
    /// </summary>
    public static class ReportRules
    {
        public const int PreviewRows = 50;
        public const int MaxExcelRows = 50_000;
        public const int MaxPrintRows = 2_000;

        /// <summary>
        /// حدّ صفحة سجل السحب (ViewReportLog): سحوبات موظفي فرعي، وكل المؤسسة لنظرة المؤسسة — كحدّ عرض الهيكل.
        /// null = الكل، 0 = لا فرع له فلا نتائج.
        /// </summary>
        public static int? LogBranchOf(Viewer viewer) => StructureScope.BranchOf(viewer);

        public static ReportExportLog Log(Viewer viewer, string key, string title, ReportFormat format, string filters, int rows) => new()
        {
            UserId = viewer.Id,
            BranchId = viewer.User.BranchId,
            ReportKey = Cut(key, 50),
            ReportTitle = Cut(title, 150),
            Format = format,
            Filters = Cut(filters, 1000),
            RowCount = rows,
            CreatedAt = DateTime.UtcNow   // تواريخ السجلات UTC كبقية النظام (الواجهة تعرضها بالتوقيت المحلي)
        };

        public static string FormatAr(ReportFormat format) => format == ReportFormat.Print ? "طباعة" : "Excel";

        private static string Cut(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

        /// <summary>
        /// يتحقق من قيم الفلاتر حسب تعريفها: المطلوب، صيغة التاريخ والعدد، وقيمة الاختيار من الخيارات المسموحة
        /// (خيارات الأقسام والفروع مبنية ضمن حدود المستخدم، فلا يُطلب بها ما هو خارجها). ويعيد نص الفلاتر المقروء.
        /// المفاتيح المجهولة تُهمل. الفلتر الأب يسبق تابعه في القائمة.
        /// </summary>
        public static (ReportFilterValues Values, string Text) Parse(IReadOnlyList<ReportFilterDto> filters, IReadOnlyDictionary<string, string?> input)
        {
            var values = new Dictionary<string, string>();
            var parts = new List<string>();

            foreach (var filter in filters)
            {
                var raw = input.TryGetValue(filter.Key, out var value) ? (value ?? string.Empty).Trim() : string.Empty;
                if (raw.Length == 0)
                {
                    if (filter.Required) throw new InvalidOperationException($"اختر «{filter.Label}»");
                    continue;
                }

                var shown = raw;
                switch (filter.Type)
                {
                    case ReportFilterType.Date:
                        if (!DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                            throw new InvalidOperationException($"«{filter.Label}» تاريخ غير صحيح");
                        break;
                    case ReportFilterType.Number:
                        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 0 || number > 100_000)
                            throw new InvalidOperationException($"«{filter.Label}» عدد صحيح بين 0 و100000");
                        break;
                    case ReportFilterType.Text:
                        if (raw.Length > 100) throw new InvalidOperationException($"«{filter.Label}» لا يتجاوز 100 حرف");
                        break;
                    default:
                        var option = filter.Options.FirstOrDefault(o => o.Value == raw)
                            ?? throw new InvalidOperationException($"قيمة «{filter.Label}» ليست من الخيارات المتاحة لك");
                        if (filter.DependsOn != null && option.Parent != null
                            && values.TryGetValue(filter.DependsOn, out var parent) && parent != option.Parent)
                            throw new InvalidOperationException($"«{option.Label}» لا يتبع ما اخترته في «{filters.First(f => f.Key == filter.DependsOn).Label}»");
                        shown = option.Label;
                        break;
                }

                values[filter.Key] = raw;
                parts.Add($"{filter.Label}: {shown}");
            }

            var result = new ReportFilterValues(values);
            if (result.Date(ReportFilters.From) is DateTime from && result.Date(ReportFilters.To) is DateTime to && from > to)
                throw new InvalidOperationException("«من تاريخ» بعد «إلى تاريخ»");

            return (result, parts.Count == 0 ? "بلا فلاتر (كل ما تسمح به صلاحيتك)" : string.Join(" · ", parts));
        }

        public static void EnsureWithinLimit(ReportTableDto table, int limit)
        {
            if (table.Total > limit)
                throw new InvalidOperationException(
                    $"في التقرير {table.Total.ToString("N0", CultureInfo.InvariantCulture)} صفاً والحد {limit.ToString("N0", CultureInfo.InvariantCulture)} — ضيّق الفلاتر (الفترة أو القسم) ثم أعد المحاولة");
            if (table.Total == 0)
                throw new InvalidOperationException("لا توجد بيانات مطابقة للفلاتر");
        }
    }

    /// <summary>بناء تعريفات الفلاتر (مشترك بين التقارير)</summary>
    public static class ReportFilters
    {
        public const string From = "from", To = "to";

        public static ReportFilterDto Date(string key, string label, DateTime? @default = null, bool required = false) => new()
        {
            Key = key, Label = label, Type = ReportFilterType.Date, Required = required,
            Default = @default?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        /// <summary>«من تاريخ» و«إلى تاريخ» بقيم افتراضية</summary>
        public static IEnumerable<ReportFilterDto> Period(DateTime from, DateTime to) =>
            [Date(From, "من تاريخ", from), Date(To, "إلى تاريخ", to)];

        public static ReportFilterDto Select(string key, string label, IEnumerable<ReportOptionDto> options,
            string? dependsOn = null, bool required = false, string? @default = null, string? hint = null) => new()
        {
            Key = key, Label = label, Type = ReportFilterType.Select, Options = options.ToList(),
            DependsOn = dependsOn, Required = required, Default = @default, Hint = hint
        };

        public static ReportFilterDto Text(string key, string label, string? hint = null) => new()
        {
            Key = key, Label = label, Type = ReportFilterType.Text, Hint = hint
        };

        public static ReportFilterDto Number(string key, string label, string? hint = null) => new()
        {
            Key = key, Label = label, Type = ReportFilterType.Number, Hint = hint
        };

        public static IEnumerable<ReportOptionDto> YesOnly(string label = "نعم") => [new("1", label)];

        /// <summary>أول السنة الحالية</summary>
        public static DateTime YearStart => new(DateTime.Today.Year, 1, 1);
        public static DateTime YearEnd => new(DateTime.Today.Year, 12, 31);
        public static DateTime MonthStart => new(DateTime.Today.Year, DateTime.Today.Month, 1);
    }
}
