using System.Globalization;
using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// تقرير في «مركز التقارير» (2026-10-12): عنوانه وصلاحية سحبه وفلاتره (خياراتها ضمن حدود المستخدم) وأعمدته،
    /// ويقرأ صفوفه من جداول ميزته ضمن الحد نفسه الذي تطبّقه صفحتها — فلا يرى أحد في التقرير أكثر مما يراه في الصفحة.
    /// تقرير جديد = صنف واحد يُسجَّل في AddInfrastructureRigstrationServices + صلاحية سحب في AppPermissions.
    /// </summary>
    public interface IReport
    {
        string Key { get; }
        string Title { get; }
        string Group { get; }
        string Description { get; }
        /// <summary>صلاحية السحب (معاينة، Excel، طباعة) — حدّها مكتوب في وصفها</summary>
        string Permission { get; }
        /// <summary>طباعة بالعرض للجداول العريضة</summary>
        bool Landscape { get; }

        Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct);
        Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct);
    }

    /// <summary>
    /// طلب تشغيل تقرير بعد التحقق من الفلاتر. Limit = أقصى الصفوف المرسلة؛ Strict = إن زاد العدد على الحد
    /// لا تُقرأ الصفوف (Excel/الطباعة تُرفض برسالة «ضيّق الفلاتر»)، وبدونه تُرسل أول Limit صفاً (المعاينة).
    /// </summary>
    public sealed record ReportRun(Viewer Viewer, ReportFilterValues Filters, int Limit, bool Strict);

    /// <summary>قيم الفلاتر بعد التحقق منها (الخيارات ضمن الحدود، التواريخ صحيحة)</summary>
    public sealed class ReportFilterValues
    {
        private readonly IReadOnlyDictionary<string, string> _values;
        public ReportFilterValues(IReadOnlyDictionary<string, string> values) => _values = values;

        public string? Text(string key) => _values.TryGetValue(key, out var v) ? v : null;
        public int? Int(string key) => int.TryParse(Text(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
        public DateTime? Date(string key) =>
            DateTime.TryParseExact(Text(key), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        /// <summary>نهاية يوم «إلى تاريخ» (لمقارنة تواريخ فيها وقت)</summary>
        public DateTime? EndOfDay(string key) => Date(key)?.AddDays(1).AddTicks(-1);
    }

    public sealed record ReportSheet(string Title, string FiltersText, string ExportedBy, DateTime ExportedAt, ReportTableDto Table);

    public interface IReportSpreadsheet
    {
        byte[] Build(ReportSheet sheet);
    }

    public interface IReportLogService
    {
        Task AddAsync(ReportExportLog log);
        /// <summary>branchId: null = كل الفروع (نظرة المؤسسة)</summary>
        Task<(List<ReportLogDto> Items, int Total)> SearchAsync(ReportLogFilterDto filter, int? branchId, int page, int pageSize);
        Task<bool> ExistsForUserAsync(int userId);
    }
}
