using Application.Common;
using Application.DTOs.Response;
using Application.Features.Pricing;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>
    /// طلبات التسعير بحالتها وعدد بنودها وعروضها والمكلَّف بها وموعدها — حدّها حدّ رؤية الطلب (PricingRules.Visible: فرع المستخدم، ومقدّم الطلب والمكلَّف).
    /// «متأخر» مرشّح متابعة (لم ينتهِ وتجاوز موعده)، لا قرار.
    /// </summary>
    public class PricingRequestsReport : ReportBase
    {
        private const string Status = "status", Text_ = "text";

        public PricingRequestsReport(DataContext db) : base(db) { }

        public override string Key => "pricing-requests";
        public override string Title => "طلبات التسعير";
        public override string Group => "مكتب التسعير";
        public override string Description => "طلبات التسعير بحالتها وعدد بنودها وعروضها المستلمة والمكلَّف بها وموعدها، مع فلتر الحالة والفترة";
        public override string Permission => "ExportPricingReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "عنوان الطلب أو المشروع"),
                Select(Status, "الحالة", Enum.GetValues<PricingRequestStatus>().Select(s => new ReportOptionDto(((int)s).ToString(), PricingRules.StatusAr(s))))
            };
            filters.AddRange(Period(YearStart, DateTime.Today));
            if (viewer.IsOrganizationWide) filters.AddRange(await UnitFiltersAsync(all: true, null, ct));
            else if (viewer.User.BranchId is int branchId) filters.AddRange(await UnitFiltersAsync(all: false, branchId, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var today = DateTime.Today;
            var query = Db.PricingRequests.AsNoTracking().Where(PricingRules.Visible(run.Viewer));

            if (Blank(f.Text(Text_)) is string text) query = query.Where(r => r.Title.Contains(text) || (r.Project != null && r.Project.Name.Contains(text)));
            if (f.Int(Status) is int status) query = query.Where(r => (int)r.Status == status);
            if (f.Date(From) is DateTime from) query = query.Where(r => r.CreatedAt >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(r => r.CreatedAt <= to);
            if (f.Int(Branch) is int branch) query = query.Where(r => r.BranchId == branch);

            var rows = query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Select(r => new
            {
                r.Id, r.CreatedAt, r.Title, r.Status, r.DueDate, r.OutsideContractScope,
                Branch = r.Branch.Name,
                Project = r.Project != null ? r.Project.Name : null,
                Items = r.Items.Count,
                Offers = r.Offers.Count,
                Assignee = r.Assignee != null ? r.Assignee.FullName : null,
                RegisteredBy = r.RegisteredByUser.FullName,
                Late = (r.Status == PricingRequestStatus.Registered || r.Status == PricingRequestStatus.Collecting || r.Status == PricingRequestStatus.Recommended)
                       && r.DueDate != null && r.DueDate < today ? 1 : 0,
                r.ResultRecipient
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("العنوان"), Col("المشروع"), Col("الفرع", print: false), Col("الحالة"), Col("خارج العقد", print: false),
                Col("البنود", Ct.Number), Col("العروض", Ct.Number), Col("المكلَّف"), Col("مقدّمه", print: false), Col("الموعد", Ct.Date),
                Col("متأخر", Ct.Number), Col("أُرسلت إلى", print: false), Col("تاريخ التسجيل", Ct.Date)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                PricingRules.Number(x.Id, x.CreatedAt), x.Title, x.Project, x.Branch, PricingRules.StatusAr(x.Status), x.OutsideContractScope ? "نعم" : "لا",
                x.Items, x.Offers, x.Assignee, x.RegisteredBy, x.DueDate?.Date, x.Late, x.ResultRecipient, x.CreatedAt.Date
            }, ct);
        }
    }

    /// <summary>
    /// محاضر الاتفاق مع المتعهدين الجدد بحالتها وإجماليها (جمع بسيط للكمية × سعر الوحدة بعملة المحضر) ومن أعدّها ومن اعتمدها — حدّها فرع المستخدم (ونظرة المؤسسة كل الفروع)،
    /// ومن لا يملك صلاحية عرض المحاضر لا يسحبها.
    /// </summary>
    public class PricingAgreementsReport : ReportBase
    {
        private const string Status = "status", Text_ = "text";

        public PricingAgreementsReport(DataContext db) : base(db) { }

        public override string Key => "pricing-agreements";
        public override string Title => "محاضر الاتفاق (التسعير)";
        public override string Group => "مكتب التسعير";
        public override string Description => "محاضر الاتفاق مع المتعهدين الجدد: المتعهد والمشروع والإجمالي بعملة المحضر والحالة ومن أعدّها ومن اعتمدها";
        public override string Permission => "ExportPricingReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "رقم المحضر أو المتعهد أو المشروع"),
                Select(Status, "الحالة", Enum.GetValues<AgreementStatus>().Select(s => new ReportOptionDto(((int)s).ToString(), PricingRules.AgreementStatusAr(s))))
            };
            filters.AddRange(Period(YearStart, DateTime.Today));
            if (viewer.IsOrganizationWide) filters.AddRange(await UnitFiltersAsync(all: true, null, ct));
            else if (viewer.User.BranchId is int branchId) filters.AddRange(await UnitFiltersAsync(all: false, branchId, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            if (!run.Viewer.IsSuperAdmin && !run.Viewer.Has(AppPermissions.ViewPricingAgreements))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض محاضر الاتفاق");

            var f = run.Filters;
            var query = Db.PricingAgreements.AsNoTracking().AsQueryable();
            if (!run.Viewer.IsOrganizationWide)
            {
                var own = run.Viewer.User.BranchId;
                query = query.Where(a => a.BranchId == own);
            }
            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(a => (a.Number != null && a.Number.Contains(text)) || a.Supplier.Name.Contains(text) || (a.Project != null && a.Project.Name.Contains(text)));
            if (f.Int(Status) is int status) query = query.Where(a => (int)a.Status == status);
            if (f.Date(From) is DateTime from) query = query.Where(a => a.AgreementDate >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(a => a.AgreementDate <= to);
            if (f.Int(Branch) is int branch) query = query.Where(a => a.BranchId == branch);

            var rows = query.OrderByDescending(a => a.AgreementDate).ThenByDescending(a => a.Id).Select(a => new
            {
                a.Id, a.CreatedAt, a.Number, a.AgreementDate, a.Currency, a.Status,
                Supplier = a.Supplier.Name,
                Project = a.Project != null ? a.Project.Name : null,
                Branch = a.Branch.Name,
                Total = a.Lines.Sum(l => (decimal?)(l.Quantity * l.UnitPrice)) ?? 0m,
                PreparedBy = a.PreparedByUser.FullName,
                ApprovedBy = a.ApprovedByUser != null ? a.ApprovedByUser.FullName : null
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("الرقم الرسمي"), Col("المتعهد"), Col("المشروع"), Col("الفرع", print: false), Col("التاريخ", Ct.Date), Col("العملة", print: false),
                Col("الإجمالي", Ct.Money), Col("الحالة"), Col("أعدّه"), Col("اعتمده")
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                PricingRules.AgreementNumber(x.Id, x.CreatedAt), x.Number, x.Supplier, x.Project, x.Branch, x.AgreementDate.Date, x.Currency, x.Total,
                PricingRules.AgreementStatusAr(x.Status), x.PreparedBy, x.ApprovedBy
            }, ct);
        }
    }
}
