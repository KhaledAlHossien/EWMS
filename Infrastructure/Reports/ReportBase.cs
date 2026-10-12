using Application.Common;
using Application.DTOs.Response;
using Application.Features.Reports;
using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Reports
{
    /// <summary>
    /// أساس تقارير المركز: قراءة العدد ثم الصفوف حسب حد التشغيل، وخيارات الفروع والأقسام ضمن حدّ التقرير.
    /// الحد نفسه يُطبَّق مرتين: في خيارات الفلاتر (لا يُختار ما هو خارجه) وفي الاستعلام (ولو لم يُختر فلتر).
    /// </summary>
    public abstract class ReportBase : IReport
    {
        protected const string Branch = "branch", Department = "department";

        protected readonly DataContext Db;

        protected ReportBase(DataContext db) => Db = db;

        public abstract string Key { get; }
        public abstract string Title { get; }
        public abstract string Group { get; }
        public abstract string Description { get; }
        public abstract string Permission { get; }
        public virtual bool Landscape => false;

        public abstract Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct);
        public abstract Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct);

        protected static ReportColumnDto Col(string header, string type = ReportColumnType.Text, bool print = true, bool sum = false) =>
            new() { Header = header, Type = type, Print = print, Sum = sum };

        protected static ReportTableDto Table(IReadOnlyList<ReportColumnDto> columns, List<object?[]> rows, int total)
        {
            for (var i = 0; i < columns.Count; i++) columns[i].Key = "c" + i;
            return new ReportTableDto { Columns = [.. columns], Rows = rows, Total = total };
        }

        /// <summary>العدد ثم الصفوف من قاعدة البيانات (لا تُقرأ الصفوف إن زاد العدد على حد Excel/الطباعة)</summary>
        protected static async Task<ReportTableDto> QueryAsync<T>(IQueryable<T> query, ReportRun run,
            IReadOnlyList<ReportColumnDto> columns, Func<T, object?[]> row, CancellationToken ct)
        {
            var total = await query.CountAsync(ct);
            var rows = run.Strict && total > run.Limit
                ? []
                : (await query.Take(run.Limit).ToListAsync(ct)).Select(row).ToList();
            return Table(columns, rows, total);
        }

        /// <summary>تقارير تُجمَّع في الذاكرة (أعداد قليلة): قصّ الصفوف حسب الحد</summary>
        protected static ReportTableDto Slice(IReadOnlyList<ReportColumnDto> columns, List<object?[]> all, ReportRun run) =>
            Table(columns, run.Strict && all.Count > run.Limit ? [] : all.Take(run.Limit).ToList(), all.Count);

        protected static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>
        /// فلترا الفرع والقسم ضمن حد (الكل / فرع / قسم): الكل → الفرع + القسم التابع له، فرع → أقسامه، قسم أو أقل → لا فلتر.
        /// </summary>
        protected async Task<List<ReportFilterDto>> UnitFiltersAsync(bool all, int? branchId, CancellationToken ct)
        {
            if (all)
            {
                var branches = await Db.Branches.AsNoTracking().OrderBy(b => b.Name)
                    .Select(b => new ReportOptionDto(b.Id.ToString(), b.Name, null)).ToListAsync(ct);
                var departments = await Db.Departments.AsNoTracking().OrderBy(d => d.Name)
                    .Select(d => new ReportOptionDto(d.Id.ToString(), d.Name + " — " + d.Branch.Name, d.BranchId.ToString())).ToListAsync(ct);
                return [ReportFilters.Select(Branch, "الفرع", branches), ReportFilters.Select(Department, "القسم", departments, dependsOn: Branch)];
            }
            if (branchId is int branch)
            {
                var departments = await Db.Departments.AsNoTracking().Where(d => d.BranchId == branch).OrderBy(d => d.Name)
                    .Select(d => new ReportOptionDto(d.Id.ToString(), d.Name, null)).ToListAsync(ct);
                return [ReportFilters.Select(Department, "القسم", departments)];
            }
            return [];
        }

        protected Task<string?> BranchNameAsync(int branchId, CancellationToken ct) =>
            Db.Branches.Where(b => b.Id == branchId).Select(b => b.Name).FirstOrDefaultAsync(ct);
    }
}
