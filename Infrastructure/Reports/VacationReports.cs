using Application.Common;
using Application.DTOs.Response;
using Application.Features.Reports;
using Application.Features.Vacations;
using Application.Interfaces;
using Domain.Enums;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Ct = Application.DTOs.Response.ReportColumnType;
using static Application.Features.Reports.ReportFilters;

namespace Infrastructure.Reports
{
    /// <summary>أسماء الأشهر كما تُكتب في سوريا (للتقارير)</summary>
    internal static class Months
    {
        public static readonly string[] Names =
            ["كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران", "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول"];

        public static string Of(int year, int month) => $"{year}-{month:D2} ({Names[month - 1]})";
    }

    /// <summary>سجل الإجازات الواقعة في فترة — حدّ VacationScope (كعرض الإجازات)</summary>
    public class VacationsReport : ReportBase
    {
        private const string Type = "type", Status = "status", Employee = "employee";

        public VacationsReport(DataContext db) : base(db) { }

        public override string Key => "vacations";
        public override string Title => "سجل الإجازات";
        public override string Group => "الإجازات";
        public override string Description => "الإجازات الواقعة في فترة: الموظف والنوع وأيام العمل والمدفوع منها والحالة ومن اعتمدها";
        public override string Permission => "ExportVacationsReport";
        public override bool Landscape => true;

        private static readonly VacationStatus[] Statuses =
            [VacationStatus.PendingManager, VacationStatus.PendingBranchManager, VacationStatus.Approved, VacationStatus.Rejected, VacationStatus.Cancelled];

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var scope = VacationScope.For(viewer);
            var filters = Period(YearStart, YearEnd).ToList();
            filters.AddRange(await UnitFiltersAsync(scope.All, scope.BranchId, ct));
            var types = await Db.VacationType.AsNoTracking().OrderBy(t => t.Name)
                .Select(t => new ReportOptionDto(t.Id.ToString(), t.Name, null)).ToListAsync(ct);
            filters.Add(Select(Type, "نوع الإجازة", types));
            filters.Add(Select(Status, "الحالة", Statuses.Select(s => new ReportOptionDto(((int)s).ToString(), VacationRules.StatusAr(s)))));
            if (scope.SeesOthers) filters.Add(Text(Employee, "الموظف", "الاسم أو الرقم الذاتي"));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var query = Db.Vacation.AsNoTracking().Where(VacationScope.For(run.Viewer).Predicate());
            if (f.Date(From) is DateTime from) query = query.Where(v => v.EndVac >= from);
            if (f.Date(To) is DateTime to) query = query.Where(v => v.StartVac <= to);
            if (f.Int(Branch) is int branch) query = query.Where(v => v.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(v => v.DepartmentId == department);
            if (f.Int(Type) is int type) query = query.Where(v => v.VacationTypeId == type);
            if (f.Int(Status) is int status) query = query.Where(v => (int)v.Status == status);
            if (Blank(f.Text(Employee)) is string employee)
            {
                var id = employee.ToUpperInvariant();
                query = query.Where(v => v.User.FullName.Contains(employee) || v.User.PersonalIdNumber == id);
            }

            var rows = query
                .OrderBy(v => v.StartVac).ThenBy(v => v.User.FullName)
                .Select(v => new
                {
                    v.Id, v.CreatedAt, Employee = v.User.FullName, v.User.PersonalIdNumber, Branch = v.Branch.Name, Department = v.Department.Name,
                    Type = v.VacationType.Name, v.StartVac, v.EndVac, v.VacDayCount, v.Status, v.PaidDays, v.UnpaidDays,
                    First = v.FirstApprovedByUser != null ? v.FirstApprovedByUser.FullName : null,
                    Final = v.FinalApprovedByUser != null ? v.FinalApprovedByUser.FullName : null, v.FinalApprovedAt,
                    Rejected = v.RejectedByUser != null ? v.RejectedByUser.FullName : null, v.RejectedAtStage, v.RejectionReason
                });

            ReportColumnDto[] columns =
            [
                Col("رقم الطلب"), Col("الموظف"), Col("الرقم الذاتي", print: false), Col("الفرع", print: false), Col("القسم"), Col("النوع"),
                Col("من", Ct.Date), Col("إلى", Ct.Date), Col("أيام العمل", Ct.Number, sum: true), Col("مدفوع", Ct.Number, sum: true), Col("غير مدفوع", Ct.Number, sum: true),
                Col("الحالة"), Col("تاريخ التقديم", Ct.Date, print: false), Col("الموافقة الأولى", print: false), Col("الاعتماد النهائي"),
                Col("تاريخ الاعتماد", Ct.Date, print: false), Col("الرفض", print: false)
            ];

            return QueryAsync(rows, run, columns, x =>
            {
                var approved = x.Status == VacationStatus.Approved;
                var rejection = x.Status == VacationStatus.Rejected
                    ? $"{(x.RejectedAtStage == VacationStatus.PendingBranchManager ? "المرحلة النهائية" : "المرحلة الأولى")} — {x.Rejected} — {x.RejectionReason}"
                    : null;
                return
                [
                    VacationRules.RequestNumber(x.Id, x.CreatedAt), x.Employee, x.PersonalIdNumber, x.Branch, x.Department, x.Type,
                    x.StartVac.Date, x.EndVac.Date, x.VacDayCount, approved ? x.PaidDays : null, approved ? x.UnpaidDays : null,
                    VacationRules.StatusAr(x.Status), x.CreatedAt.Date, x.First, x.Final, x.FinalApprovedAt?.Date, rejection
                ];
            }, ct);
        }
    }

    /// <summary>استهلاك كل موظف في كل شهر من الإجازات المعتمدة (أجزاء الأشهر VacationSegment) — حدّ VacationScope</summary>
    public class VacationUsageReport : ReportBase
    {
        private const string Year = "year", Month = "month", Type = "type", Employee = "employee";

        public VacationUsageReport(DataContext db) : base(db) { }

        public override string Key => "vacation-usage";
        public override string Title => "استهلاك الإجازات الشهري";
        public override string Group => "الإجازات";
        public override string Description => "لكل موظف وشهر: أيام العمل المدفوعة وغير المدفوعة وعدد الإجازات، من الإجازات المعتمدة";
        public override string Permission => "ExportVacationUsageReport";

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var scope = VacationScope.For(viewer);
            var thisYear = DateTime.Today.Year;
            var first = await Db.VacationSegments.MinAsync(s => (int?)s.StartDate.Year, ct) ?? thisYear;
            var years = Enumerable.Range(Math.Min(first, thisYear), thisYear + 1 - Math.Min(first, thisYear) + 1).Reverse()
                .Select(y => new ReportOptionDto(y.ToString(), y.ToString()));

            var filters = new List<ReportFilterDto>
            {
                Select(Year, "السنة", years, required: true, @default: thisYear.ToString()),
                Select(Month, "الشهر", Months.Names.Select((name, i) => new ReportOptionDto((i + 1).ToString(), name)))
            };
            filters.AddRange(await UnitFiltersAsync(scope.All, scope.BranchId, ct));
            var types = await Db.VacationType.AsNoTracking().OrderBy(t => t.Name)
                .Select(t => new ReportOptionDto(t.Id.ToString(), t.Name, null)).ToListAsync(ct);
            filters.Add(Select(Type, "نوع الإجازة", types));
            if (scope.SeesOthers) filters.Add(Text(Employee, "الموظف", "الاسم أو الرقم الذاتي"));
            return filters;
        }

        public override async Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var year = f.Int(Year) ?? DateTime.Today.Year;
            var vacations = Db.Vacation.AsNoTracking().Where(VacationScope.For(run.Viewer).Predicate())
                .Where(v => v.Status == VacationStatus.Approved);
            if (f.Int(Branch) is int branch) vacations = vacations.Where(v => v.BranchId == branch);
            if (f.Int(Department) is int department) vacations = vacations.Where(v => v.DepartmentId == department);
            if (f.Int(Type) is int type) vacations = vacations.Where(v => v.VacationTypeId == type);
            if (Blank(f.Text(Employee)) is string employee)
            {
                var id = employee.ToUpperInvariant();
                vacations = vacations.Where(v => v.User.FullName.Contains(employee) || v.User.PersonalIdNumber == id);
            }

            var segments = vacations.SelectMany(v => v.Segments, (v, s) => new
            {
                VacationId = v.Id, v.UserId, Employee = v.User.FullName, v.User.PersonalIdNumber, Department = v.Department.Name,
                s.StartDate, s.IsPaid, s.Days
            }).Where(x => x.StartDate.Year == year);
            if (f.Int(Month) is int month) segments = segments.Where(x => x.StartDate.Month == month);

            var rows = (await segments.ToListAsync(ct))
                .GroupBy(x => new { x.UserId, x.Employee, x.PersonalIdNumber, x.Department, x.StartDate.Month })
                .OrderBy(g => g.Key.Employee).ThenBy(g => g.Key.Month)
                .Select(g =>
                {
                    var paid = g.Where(x => x.IsPaid).Sum(x => x.Days);
                    var unpaid = g.Where(x => !x.IsPaid).Sum(x => x.Days);
                    return new object?[]
                    {
                        g.Key.Employee, g.Key.PersonalIdNumber, g.Key.Department, Months.Of(year, g.Key.Month),
                        paid, unpaid, paid + unpaid, g.Select(x => x.VacationId).Distinct().Count()
                    };
                }).ToList();

            ReportColumnDto[] columns =
            [
                Col("الموظف"), Col("الرقم الذاتي", print: false), Col("القسم"), Col("الشهر"),
                Col("أيام مدفوعة", Ct.Number, sum: true), Col("أيام غير مدفوعة", Ct.Number, sum: true), Col("المجموع", Ct.Number, sum: true),
                Col("إجازات في الشهر", Ct.Number)
            ];
            return Slice(columns, rows, run);
        }
    }
}
