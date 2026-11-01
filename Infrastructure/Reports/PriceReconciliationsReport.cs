using Application.Common;
using Application.DTOs.Response;
using Application.Features.Reconciliation;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>
    /// مطابقات الفواتير والكشوف بحالتها (إحدى الثماني) وإجمالي المستند بعملته ومن دقّق ومن اعتمد وإحالتها — حدّها فرع المستخدم (ونظرة المؤسسة كل الفروع)،
    /// ومن لا يملك صلاحية عرض المطابقات لا يسحبها. «الفواتير والكشوف التي تحتاج إلى تدقيق» = حالتا مسودة ومعاد للتصحيح وما بانتظار الاعتماد.
    /// </summary>
    public class PriceReconciliationsReport : ReportBase
    {
        private const string Status = "status", Text_ = "text";

        public PriceReconciliationsReport(DataContext db) : base(db) { }

        public override string Key => "price-reconciliations";
        public override string Title => "مطابقة الفواتير والكشوف";
        public override string Group => "مكتب التسعير";
        public override string Description => "الفواتير والكشوف الواردة ومطابقتها مع الأسعار: المورد والإجمالي والحالة ومن دقّق ومن اعتمد والإحالة، مع فلتر الحالة والفترة";
        public override string Permission => "ExportPricingReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "رقم المستند أو المورد أو المشروع"),
                Select(Status, "الحالة", Enum.GetValues<ReconciliationStatus>().Select(s => new ReportOptionDto(((int)s).ToString(), ReconciliationRules.StatusAr(s))))
            };
            filters.AddRange(Period(YearStart, DateTime.Today));
            if (viewer.IsOrganizationWide) filters.AddRange(await UnitFiltersAsync(all: true, null, ct));
            else if (viewer.User.BranchId is int branchId) filters.AddRange(await UnitFiltersAsync(all: false, branchId, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            ReconciliationRules.EnsureCanViewAny(run.Viewer);

            var f = run.Filters;
            var query = Db.PriceReconciliations.AsNoTracking().AsQueryable();
            if (!run.Viewer.IsOrganizationWide)
            {
                var own = run.Viewer.User.BranchId;
                query = query.Where(r => r.BranchId == own);
            }
            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(r => r.DocumentNumber.Contains(text) || r.Supplier.Name.Contains(text) || (r.Project != null && r.Project.Name.Contains(text)));
            if (f.Int(Status) is int status) query = query.Where(r => (int)r.Status == status);
            if (f.Date(From) is DateTime from) query = query.Where(r => r.DocumentDate >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(r => r.DocumentDate <= to);
            if (f.Int(Branch) is int branch) query = query.Where(r => r.BranchId == branch);

            var rows = query.OrderByDescending(r => r.DocumentDate).ThenByDescending(r => r.Id).Select(r => new
            {
                r.Id, r.CreatedAt, r.DocumentKind, r.DocumentNumber, r.DocumentDate, r.Currency, r.Status, r.ReferredTo,
                Supplier = r.Supplier.Name,
                Project = r.Project != null ? r.Project.Name : null,
                Branch = r.Branch.Name,
                Total = r.Lines.Sum(l => (decimal?)(l.InvoiceQuantity * l.InvoiceUnitPrice)) ?? 0m,
                PreparedBy = r.PreparedByUser.FullName,
                DecidedBy = r.DecidedByUser != null ? r.DecidedByUser.FullName : null
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("النوع"), Col("رقم المستند"), Col("المورد"), Col("المشروع"), Col("الفرع", print: false), Col("تاريخ المستند", Ct.Date), Col("العملة", print: false),
                Col("الإجمالي", Ct.Money), Col("الحالة"), Col("دقّقه"), Col("اعتمده"), Col("أُحيلت إلى", print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                ReconciliationRules.Number(x.Id, x.CreatedAt), ReconciliationRules.KindAr(x.DocumentKind), x.DocumentNumber, x.Supplier, x.Project, x.Branch, x.DocumentDate.Date, x.Currency,
                x.Total, ReconciliationRules.StatusAr(x.Status), x.PreparedBy, x.DecidedBy, x.ReferredTo
            }, ct);
        }
    }
}
