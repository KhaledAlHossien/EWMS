using Application.Common;
using Application.DTOs.Response;
using Application.Features.Contracts;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>
    /// العقود بحالتها وطبيعتها والمتعاقد معه وقيمتها بعد الملاحق وضمانها والمحتجز غير المفرج عنه — حدّها حدّ رؤية المشروع (ProjectRules.Visible) كحدّ العقد نفسه.
    /// القيم أرقام أُدخلت يدوياً (جمع بسيط للملاحق والمحتجز)، و«ينتهي ضمانه قريباً» مرشّح متابعة لا قرار.
    /// </summary>
    public class ContractsReport : ReportBase
    {
        private const string Status = "status", Method = "method", Contractor = "contractor", Text_ = "text";

        public ContractsReport(DataContext db) : base(db) { }

        public override string Key => "contracts";
        public override string Title => "العقود";
        public override string Group => "المشاريع";
        public override string Description => "العقود بحالتها وطبيعة التعاقد والمتعاقد معه وقيمتها بعد الملاحق، مع الضمان (نهايته وأيامه) والمبالغ المحتجزة غير المفرج عنها";
        public override string Permission => "ExportContractsReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "رقم العقد أو المشروع أو المتعاقد معه"),
                Select(Status, "الحالة", Enum.GetValues<ContractStatus>().Select(s => new ReportOptionDto(((int)s).ToString(), ContractRules.StatusAr(s)))),
                Select(Method, "طبيعة التعاقد", Enum.GetValues<ContractingMethod>().Select(m => new ReportOptionDto(((int)m).ToString(), ContractRules.MethodAr(m))))
            };
            filters.AddRange(Period(YearStart, DateTime.Today));

            if (viewer.IsOrganizationWide)
                filters.AddRange(await UnitFiltersAsync(all: true, null, ct));
            else if (viewer.Has(AppPermissions.ViewBranchProjects) && viewer.User.BranchId is int branchId)
                filters.AddRange(await UnitFiltersAsync(all: false, branchId, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var today = DateTime.Today;
            var visibleProjects = Db.Projects.Where(ProjectRules.Visible(run.Viewer)).Select(p => p.Id);
            var query = Db.Contracts.AsNoTracking().Where(c => visibleProjects.Contains(c.ProjectId));

            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(c => (c.Number != null && c.Number.Contains(text)) || c.Project.Name.Contains(text) || (c.Contractor != null && c.Contractor.Name.Contains(text)));
            if (f.Int(Status) is int status) query = query.Where(c => (int)c.Status == status);
            if (f.Int(Method) is int method) query = query.Where(c => (int)c.Method == method);
            if (f.Date(From) is DateTime from) query = query.Where(c => c.CreatedAt >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(c => c.CreatedAt <= to);
            if (f.Int(Branch) is int branch) query = query.Where(c => c.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(c => c.Project.DepartmentId == department);

            var rows = query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Select(c => new
            {
                c.Id, c.CreatedAt, c.Number, c.Method, c.Status, c.ContractDate, c.Value, c.Currency, c.WarrantyEnd,
                Project = c.Project.Name,
                Branch = c.Project.Branch.Name,
                Contractor = c.Contractor != null ? c.Contractor.Name : null,
                ValueChange = c.Amendments.Sum(a => a.ValueChange ?? 0m),
                Amendments = c.Amendments.Count,
                Withheld = c.RetentionEntries.Where(e => !e.Decisions.Any(d => d.Decision == RetentionDecisionKind.Release)).Sum(e => (decimal?)e.Amount) ?? 0m
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("الرقم الرسمي"), Col("المشروع"), Col("الفرع", print: false), Col("الطبيعة"), Col("الحالة"), Col("المتعاقد معه"),
                Col("العملة", print: false), Col("القيمة", Ct.Money), Col("القيمة بعد الملاحق", Ct.Money), Col("الملاحق", Ct.Number, print: false),
                Col("تاريخ العقد", Ct.Date), Col("نهاية الضمان", Ct.Date), Col("أيام الضمان المتبقية", Ct.Number), Col("محتجز غير مفرج عنه", Ct.Money)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                ContractRules.Number(x.Id, x.CreatedAt), x.Number, x.Project, x.Branch, ContractRules.MethodAr(x.Method), ContractRules.StatusAr(x.Status), x.Contractor,
                x.Currency, x.Value, x.Value is decimal v ? v + x.ValueChange : null, x.Amendments,
                x.ContractDate?.Date, x.WarrantyEnd?.Date, x.WarrantyEnd is DateTime w ? (int?)(w.Date - today).Days : null, x.Withheld
            }, ct);
        }
    }

    /// <summary>
    /// تقييمات المقاولين المراجَعة المحفوظة في ملفاتهم (درجات يدوية بلا مجموع ولا تصنيف). حدّها حدّ رؤية مشروع العقد، ومن لا يملك «عرض التقييمات» لا يسحبها.
    /// </summary>
    public class ContractorEvaluationsReport : ReportBase
    {
        private const string Contractor = "contractor", Text_ = "text";

        public ContractorEvaluationsReport(DataContext db) : base(db) { }

        public override string Key => "contractor-evaluations";
        public override string Title => "تقييمات المقاولين";
        public override string Group => "المشاريع";
        public override string Description => "التقييمات المراجَعة المحفوظة في ملفات المقاولين: المقاول والعقد والمشروع والمُقيِّم والمراجع وعدد المعايير المُقيَّمة (درجات يدوية بلا مجموع ولا تصنيف)";
        public override string Permission => "ExportContractsReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto> { Text(Text_, "بحث", "اسم المقاول أو المشروع") };
            filters.AddRange(Period(YearStart, DateTime.Today));
            if (viewer.IsOrganizationWide)
                filters.AddRange(await UnitFiltersAsync(all: true, null, ct));
            else if (viewer.Has(AppPermissions.ViewBranchProjects) && viewer.User.BranchId is int branchId)
                filters.AddRange(await UnitFiltersAsync(all: false, branchId, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            if (!run.Viewer.IsSuperAdmin && !run.Viewer.Has(AppPermissions.ViewContractorEvaluations))
                throw new UnauthorizedAccessException("لا تملك صلاحية عرض تقييمات المقاولين");

            var f = run.Filters;
            var visibleProjects = Db.Projects.Where(ProjectRules.Visible(run.Viewer)).Select(p => p.Id);
            var query = Db.ContractorEvaluations.AsNoTracking()
                .Where(e => e.Status == EvaluationStatus.Reviewed && visibleProjects.Contains(e.Contract.ProjectId));

            if (Blank(f.Text(Text_)) is string text) query = query.Where(e => e.Contractor.Name.Contains(text) || e.Contract.Project.Name.Contains(text));
            if (f.Date(From) is DateTime from) query = query.Where(e => e.EvaluationDate >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(e => e.EvaluationDate <= to);
            if (f.Int(Branch) is int branch) query = query.Where(e => e.Contract.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(e => e.Contract.Project.DepartmentId == department);

            var rows = query.OrderByDescending(e => e.EvaluationDate).ThenByDescending(e => e.Id).Select(e => new
            {
                e.Id, e.EvaluationDate, e.ContractId, ContractCreated = e.Contract.CreatedAt, ContractNumber = e.Contract.Number,
                Contractor = e.Contractor.Name, Project = e.Contract.Project.Name,
                Evaluator = e.EvaluatedByUser.FullName, Reviewer = e.ReviewedByUser != null ? e.ReviewedByUser.FullName : null,
                Scored = e.Scores.Count(s => s.Score != null), Total = e.Scores.Count, e.Notes
            });

            ReportColumnDto[] columns =
            [
                Col("المقاول"), Col("العقد"), Col("الرقم الرسمي"), Col("المشروع"), Col("تاريخ التقييم", Ct.Date), Col("المُقيِّم"), Col("المراجع"),
                Col("المعايير المُقيَّمة", Ct.Number), Col("إجمالي المعايير", Ct.Number, print: false), Col("الملاحظات", print: false)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                x.Contractor, ContractRules.Number(x.ContractId, x.ContractCreated), x.ContractNumber, x.Project, x.EvaluationDate.Date, x.Evaluator, x.Reviewer,
                x.Scored, x.Total, x.Notes
            }, ct);
        }
    }
}
