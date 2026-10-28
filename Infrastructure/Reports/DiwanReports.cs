using Application.Common;
using Application.DTOs.Response;
using Application.Features.Diwan;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>خيارات مشتركة لتقارير الديوان ضمن حدّ المستخدم (حدّ رؤية المعاملات DiwanRules.Visible)</summary>
    public abstract class DiwanReportBase(DataContext db) : ReportBase(db)
    {
        protected const string Type = "type", Stage = "stage", Direction = "direction", Text_ = "text";

        protected async Task<ReportFilterDto> TypeFilterAsync(Viewer viewer, CancellationToken ct)
        {
            var query = Db.CorrespondenceTypes.AsNoTracking().AsQueryable();
            if (!viewer.IsOrganizationWide) query = query.Where(t => t.BranchId == viewer.User.BranchId);
            var types = await query.OrderBy(t => t.Branch.Name).ThenBy(t => t.Name)
                .Select(t => new ReportOptionDto(t.Id.ToString(), viewer.IsOrganizationWide ? t.Name + " — " + t.Branch.Name : t.Name, null)).ToListAsync(ct);
            return Select(Type, "النوع", types);
        }

        protected async Task<List<ReportFilterDto>> UnitFiltersForAsync(Viewer viewer, CancellationToken ct)
        {
            if (viewer.IsOrganizationWide) return await UnitFiltersAsync(all: true, null, ct);
            if (viewer.Has(AppPermissions.ViewBranchCorrespondence) && viewer.User.BranchId is int branchId) return await UnitFiltersAsync(all: false, branchId, ct);
            return [];
        }

        /// <summary>الجهة الحاملة الآن: مكتب إن وُجد وإلا قسم، مع الموظف المسمّى إن وُجد</summary>
        protected static string Holder(string? office, string? department, string? user) =>
            string.Join(" — ", new[] { office ?? department, user }.Where(x => !string.IsNullOrEmpty(x))) is { Length: > 0 } text ? text : "—";
    }

    /// <summary>
    /// سجل المعاملات الواردة والصادرة بحالتها ووحدتها الحالية وموعدها، مع فلاتر الاتجاه والنوع والمرحلة والفترة — حدّه حدّ رؤية المعاملات:
    /// معاملاتي ← قسمي ← فرعي ← المؤسسة (ومن يعالج في وحدته الحالية). الصادر المسودة يظهر بلا رقم.
    /// </summary>
    public class CorrespondenceRegisterReport : DiwanReportBase
    {
        public CorrespondenceRegisterReport(DataContext db) : base(db) { }

        public override string Key => "correspondence";
        public override string Title => "سجل المعاملات";
        public override string Group => "الديوان";
        public override string Description => "المعاملات الواردة والصادرة بأرقامها وحالتها ووحدتها الحالية وموعدها، مع فلتر الاتجاه والنوع والمرحلة والفترة";
        public override string Permission => "ExportCorrespondenceReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "موضوع أو جهة أو رقم كتاب"),
                Select(Direction, "الاتجاه", [new ReportOptionDto("1", "وارد"), new ReportOptionDto("2", "صادر")]),
                await TypeFilterAsync(viewer, ct),
                Select(Stage, "المرحلة", Enum.GetValues<CorrespondenceStage>().Select(s => new ReportOptionDto(((int)s).ToString(), DiwanRules.StageAr(s))))
            };
            filters.AddRange(Period(YearStart, DateTime.Today));
            filters.AddRange(await UnitFiltersForAsync(viewer, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var today = DateTime.Today;
            var query = Db.Correspondences.AsNoTracking().Where(DiwanRules.Visible(run.Viewer));

            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(c => c.Subject.Contains(text) || c.Party.Contains(text) || (c.ExternalNumber != null && c.ExternalNumber.Contains(text)));
            if (f.Int(Direction) is int direction) query = query.Where(c => (int)c.Direction == direction);
            if (f.Int(Type) is int type) query = query.Where(c => c.TypeId == type);
            if (f.Int(Stage) is int stage) query = query.Where(c => c.Status != null && (int)c.Status.Stage == stage);
            if (f.Date(From) is DateTime from) query = query.Where(c => c.RegisteredAt >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(c => c.RegisteredAt <= to);
            if (f.Int(Branch) is int branch) query = query.Where(c => c.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(c => c.DepartmentId == department || c.CurrentDepartmentId == department);

            var rows = query.OrderByDescending(c => c.RegisteredAt).ThenByDescending(c => c.Id).Select(c => new
            {
                c.Direction, c.Year, c.Sequence, c.Subject, c.Party, c.ExternalNumber, c.DueDate, c.RegisteredAt, c.OutgoingState,
                Branch = c.Branch.Name,
                TypeName = c.Type.Name,
                Status = c.Status != null ? c.Status.Name : null,
                StageValue = c.Status != null ? (CorrespondenceStage?)c.Status.Stage : null,
                Office = c.CurrentOffice != null ? c.CurrentOffice.Name : null,
                DepartmentName = c.CurrentDepartment != null ? c.CurrentDepartment.Name : null,
                User = c.CurrentUser != null ? c.CurrentUser.FullName : null,
                Project = c.Project != null ? c.Project.Name : null,
                RegisteredBy = c.RegisteredByUser.FullName
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("الاتجاه"), Col("الموضوع"), Col("الجهة"), Col("رقم الكتاب"), Col("النوع"), Col("الفرع", print: false),
                Col("الحالة"), Col("الجهة الحاملة"), Col("الموعد", Ct.Date), Col("متأخرة"), Col("المشروع", print: false),
                Col("سجّلها", print: false), Col("تاريخ التسجيل", Ct.Date)
            ];
            return QueryAsync(rows, run, columns, x =>
            {
                var incoming = x.Direction == CorrespondenceDirection.Incoming;
                var late = incoming && x.DueDate is DateTime due && due.Date < today && x.StageValue is CorrespondenceStage.Registered or CorrespondenceStage.InProgress;
                return new object?[]
                {
                    DiwanRules.Number(x.Direction, x.Year, x.Sequence) ?? "مسودة", DiwanRules.DirectionAr(x.Direction), x.Subject, x.Party,
                    x.ExternalNumber, x.TypeName, x.Branch,
                    incoming ? x.Status ?? "—" : x.OutgoingState is OutgoingState state ? DiwanRules.OutgoingStateAr(state) : "—",
                    incoming ? Holder(x.Office, x.DepartmentName, x.User) : "—",
                    x.DueDate?.Date, late ? "نعم" : "لا", x.Project ?? "—", x.RegisteredBy, x.RegisteredAt.Date
                };
            }, ct);
        }
    }

    /// <summary>
    /// المعاملات الواردة المفتوحة التي تجاوزت مهلتها (مرشّحة للتأخر: تنبيه لا قرار)، الأقدم موعداً أولاً مع أيام التأخر ووحدتها الحالية —
    /// ضمن حدّ رؤية المعاملات.
    /// </summary>
    public class OverdueCorrespondenceReport : DiwanReportBase
    {
        public OverdueCorrespondenceReport(DataContext db) : base(db) { }

        public override string Key => "correspondence-overdue";
        public override string Title => "معاملات تجاوزت المدة";
        public override string Group => "الديوان";
        public override string Description => "الوارد المفتوح الذي تجاوز موعد معالجته، الأقدم أولاً، بأيام التأخر والجهة الحاملة";
        public override string Permission => "ExportOverdueCorrespondenceReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto> { Text(Text_, "بحث", "موضوع أو جهة أو رقم كتاب"), await TypeFilterAsync(viewer, ct) };
            filters.AddRange(await UnitFiltersForAsync(viewer, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var today = DateTime.Today;
            var query = Db.Correspondences.AsNoTracking().Where(DiwanRules.Visible(run.Viewer))
                .Where(c => c.Direction == CorrespondenceDirection.Incoming && c.DueDate != null && c.DueDate < today
                            && c.Status != null && (c.Status.Stage == CorrespondenceStage.Registered || c.Status.Stage == CorrespondenceStage.InProgress));

            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(c => c.Subject.Contains(text) || c.Party.Contains(text) || (c.ExternalNumber != null && c.ExternalNumber.Contains(text)));
            if (f.Int(Type) is int type) query = query.Where(c => c.TypeId == type);
            if (f.Int(Branch) is int branch) query = query.Where(c => c.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(c => c.DepartmentId == department || c.CurrentDepartmentId == department);

            var rows = query.OrderBy(c => c.DueDate).ThenBy(c => c.Id).Select(c => new
            {
                c.Direction, c.Year, c.Sequence, c.Subject, c.Party, c.ExternalNumber, c.DueDate, c.RegisteredAt,
                Branch = c.Branch.Name,
                TypeName = c.Type.Name,
                Status = c.Status!.Name,
                Office = c.CurrentOffice != null ? c.CurrentOffice.Name : null,
                DepartmentName = c.CurrentDepartment != null ? c.CurrentDepartment.Name : null,
                User = c.CurrentUser != null ? c.CurrentUser.FullName : null,
                RegisteredBy = c.RegisteredByUser.FullName
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("الموضوع"), Col("الجهة"), Col("رقم الكتاب"), Col("النوع"), Col("الفرع", print: false), Col("الحالة"),
                Col("الجهة الحاملة"), Col("الموعد", Ct.Date), Col("أيام التأخر", Ct.Number, sum: false), Col("سجّلها", print: false), Col("تاريخ التسجيل", Ct.Date)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                DiwanRules.Number(x.Direction, x.Year, x.Sequence), x.Subject, x.Party, x.ExternalNumber, x.TypeName, x.Branch, x.Status,
                Holder(x.Office, x.DepartmentName, x.User), x.DueDate!.Value.Date, (today - x.DueDate.Value.Date).Days, x.RegisteredBy, x.RegisteredAt.Date
            }, ct);
        }
    }
}
