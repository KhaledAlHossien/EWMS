using Application.Common;
using Application.DTOs.Response;
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
    /// قائمة المشاريع حسب المرحلة — حدّها حدّ رؤية المشاريع (ProjectRules.Visible): مشاريعي ← قسمي ← فرعي ← المؤسسة.
    /// فلاتر الفرع والقسم تُبنى ضمن ما يراه المستخدم (من يرى مشاريع فرعه فقط يختار القسم، ومن يرى مشاريعه وحدها لا فلاتر وحدات له).
    /// </summary>
    public class ProjectsReport : ReportBase
    {
        private const string Stage = "stage", Site = "site", Text_ = "text";

        public ProjectsReport(DataContext db) : base(db) { }

        public override string Key => "projects";
        public override string Title => "المشاريع";
        public override string Group => "المشاريع";
        public override string Description => "قائمة المشاريع بمرحلتها وقسمها وموقعها ومشاركيها، مع فلتر المرحلة والفترة";
        public override string Permission => "ExportProjectsReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "اسم أو رقم مرجعي أو جهة مستفيدة"),
                Select(Stage, "المرحلة", Enum.GetValues<ProjectStage>().Select(s => new ReportOptionDto(((int)s).ToString(), ProjectRules.StageAr(s)))),
                Text(Site, "الموقع", "جزء من اسم الموقع")
            };
            filters.AddRange(Period(YearStart, DateTime.Today));

            // وحدات الفلترة ضمن حدّ المستخدم: الكل / فرعه / لا شيء (حدّ قسمي أو مشاريعي يكفيه بحث الاسم)
            if (viewer.IsOrganizationWide)
                filters.AddRange(await UnitFiltersAsync(all: true, null, ct));
            else if (viewer.Has(AppPermissions.ViewBranchProjects) && viewer.User.BranchId is int branchId)
                filters.AddRange(await UnitFiltersAsync(all: false, branchId, ct));
            return filters;
        }

        public override Task<ReportTableDto> RunAsync(ReportRun run, CancellationToken ct)
        {
            var f = run.Filters;
            var query = Db.Projects.AsNoTracking().Where(ProjectRules.Visible(run.Viewer));

            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(p => p.Name.Contains(text) || p.Beneficiary.Contains(text)
                                         || (p.ReferenceNumber != null && p.ReferenceNumber.Contains(text)));
            if (f.Int(Stage) is int stage) query = query.Where(p => (int)p.Stage == stage);
            if (Blank(f.Text(Site)) is string site) query = query.Where(p => p.Site != null && p.Site.Name.Contains(site));
            if (f.Date(From) is DateTime from) query = query.Where(p => p.CreatedAt >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(p => p.CreatedAt <= to);
            if (f.Int(Branch) is int branch) query = query.Where(p => p.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(p => p.DepartmentId == department);

            var rows = query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).Select(p => new
            {
                p.Id, p.CreatedAt, p.ReferenceNumber, p.Name, p.Beneficiary, p.Stage,
                Branch = p.Branch.Name,
                Department = p.Department != null ? p.Department.Name : null,
                Site = p.Site != null ? p.Site.Name : null,
                Governorate = p.Site != null ? p.Site.GovernorateCode : null,
                Members = p.Members.Count,
                CreatedBy = p.CreatedByUser.FullName
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("الرقم المرجعي"), Col("المشروع"), Col("الجهة المستفيدة"), Col("الفرع"), Col("القسم"),
                Col("الموقع"), Col("المحافظة", print: false), Col("المرحلة"), Col("المشاركون", Ct.Number, sum: false),
                Col("أنشأه", print: false), Col("تاريخ الإنشاء", Ct.Date)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                ProjectRules.Number(x.Id, x.CreatedAt), x.ReferenceNumber, x.Name, Blank(x.Beneficiary), x.Branch, x.Department ?? "—",
                x.Site ?? "—", x.Governorate == null ? null : Governorates.NameOf(x.Governorate), ProjectRules.StageAr(x.Stage), x.Members,
                x.CreatedBy, x.CreatedAt.Date
            }, ct);
        }
    }
}
