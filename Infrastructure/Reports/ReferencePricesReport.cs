using Application.Common;
using Application.DTOs.Response;
using Application.Features.ReferencePrices;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>
    /// الأسعار المرجعية بمصدرها وتاريخها وحالتها (تاريخي / معتمد للاستخدام الحالي / مُستبدَل) — حدّها فرع المستخدم (ونظرة المؤسسة كل الفروع)،
    /// ومن لا يملك صلاحية عرض الأسعار المرجعية لا يسحبها. السجل التاريخي يُعرض كما هو: لا أعلى ولا أقل، ولا جمع بين وحدات أو عملات.
    /// </summary>
    public class ReferencePricesReport : ReportBase
    {
        private const string Status = "status", Text_ = "text";

        public ReferencePricesReport(DataContext db) : base(db) { }

        public override string Key => "reference-prices";
        public override string Title => "الأسعار المرجعية";
        public override string Group => "مكتب التسعير";
        public override string Description => "الأسعار المرجعية للمواد: المادة والمورد وسعر الوحدة بوحدته وعملته وتاريخه ومصدره وحالته، مع فلتر الحالة والفترة";
        public override string Permission => "ExportPricingReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "رمز المادة أو اسمها أو المورد أو المصدر"),
                Select(Status, "الحالة", Enum.GetValues<ReferencePriceStatus>().Select(s => new ReportOptionDto(((int)s).ToString(), ReferencePriceRules.StatusAr(s))))
            };
            filters.AddRange(Period(YearStart, DateTime.Today));
            if (viewer.IsOrganizationWide) filters.AddRange(await UnitFiltersAsync(all: true, null, ct));
            else if (viewer.User.BranchId is int branchId) filters.AddRange(await UnitFiltersAsync(all: false, branchId, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            ReferencePriceRules.EnsureCanViewAny(run.Viewer);

            var f = run.Filters;
            var query = Db.ReferencePrices.AsNoTracking().AsQueryable();
            if (!run.Viewer.IsOrganizationWide)
            {
                var own = run.Viewer.User.BranchId;
                query = query.Where(p => p.BranchId == own);
            }
            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(p => (p.ItemCode != null && p.ItemCode.Contains(text)) || p.ItemName.Contains(text) || p.Supplier.Name.Contains(text) || p.SourceDocument.Contains(text));
            if (f.Int(Status) is int status) query = query.Where(p => (int)p.Status == status);
            if (f.Date(From) is DateTime from) query = query.Where(p => p.PriceDate >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(p => p.PriceDate <= to);
            if (f.Int(Branch) is int branch) query = query.Where(p => p.BranchId == branch);

            var rows = query.OrderByDescending(p => p.PriceDate).ThenByDescending(p => p.Id).Select(p => new
            {
                p.Id, p.CreatedAt, p.ItemCode, p.ItemName, p.Unit, p.UnitPrice, p.Currency, p.PriceDate, p.Source, p.SourceDocument, p.Status,
                Supplier = p.Supplier.Name,
                Branch = p.Branch.Name,
                Project = p.Project != null ? p.Project.Name : null,
                CreatedBy = p.CreatedByUser.FullName,
                ApprovedBy = p.ApprovedByUser != null ? p.ApprovedByUser.FullName : null
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("الرمز"), Col("المادة"), Col("المورد"), Col("الفرع", print: false), Col("الوحدة"), Col("سعر الوحدة", Ct.Money), Col("العملة"),
                Col("تاريخ السعر", Ct.Date), Col("المصدر"), Col("مستند المصدر"), Col("المشروع", print: false), Col("الحالة"), Col("أضافه", print: false), Col("اعتمده", print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                ReferencePriceRules.Number(x.Id, x.CreatedAt), x.ItemCode, x.ItemName, x.Supplier, x.Branch, x.Unit, x.UnitPrice, x.Currency, x.PriceDate.Date,
                ReferencePriceRules.SourceAr(x.Source), x.SourceDocument, x.Project, ReferencePriceRules.StatusAr(x.Status), x.CreatedBy, x.ApprovedBy
            }, ct);
        }
    }
}
