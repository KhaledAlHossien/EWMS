using Application.Common;
using Application.DTOs.Response;
using Application.Features.Projects;
using Application.Features.Studies;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using static Application.Features.Reports.ReportFilters;
using Ct = Application.DTOs.Response.ReportColumnType;

namespace Infrastructure.Reports
{
    /// <summary>
    /// أوامر الدراسة بحالتها وتقدّم مخرجاتها وجولات إرسالها للمركزية — حدّها حدّ رؤية المشروع (ProjectRules.Visible) كحدّ الأمر نفسه.
    /// «مخرجات متأخرة» مرشّحات متابعة (لم تُعتمد وتجاوزت موعدها في أمر مفتوح)، لا قرار.
    /// </summary>
    public class StudiesReport : ReportBase
    {
        private const string Status = "status", Text_ = "text";

        public StudiesReport(DataContext db) : base(db) { }

        public override string Key => "studies";
        public override string Title => "الدراسات";
        public override string Group => "المشاريع";
        public override string Description => "أوامر الدراسة بحالتها وتقدّم مخرجاتها المعتمدة وجولات إرسالها إلى المركزية، مع فلتر الحالة والفترة";
        public override string Permission => "ExportStudiesReport";
        public override bool Landscape => true;

        public override async Task<List<ReportFilterDto>> GetFiltersAsync(Viewer viewer, CancellationToken ct)
        {
            var filters = new List<ReportFilterDto>
            {
                Text(Text_, "بحث", "عنوان أو مشروع أو رقم الأمر"),
                Select(Status, "الحالة", Enum.GetValues<StudyStatus>().Select(s => new ReportOptionDto(((int)s).ToString(), StudyRules.StatusAr(s))))
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
            var query = Db.StudyOrders.AsNoTracking().Where(o => visibleProjects.Contains(o.ProjectId));

            if (Blank(f.Text(Text_)) is string text)
                query = query.Where(o => o.Title.Contains(text) || o.Project.Name.Contains(text) || (o.ExternalNumber != null && o.ExternalNumber.Contains(text)));
            if (f.Int(Status) is int status) query = query.Where(o => (int)o.Status == status);
            if (f.Date(From) is DateTime from) query = query.Where(o => o.CreatedAt >= from);
            if (f.EndOfDay(To) is DateTime to) query = query.Where(o => o.CreatedAt <= to);
            if (f.Int(Branch) is int branch) query = query.Where(o => o.Project.BranchId == branch);
            if (f.Int(Department) is int department) query = query.Where(o => o.Project.DepartmentId == department);

            var rows = query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id).Select(o => new
            {
                o.Id, o.CreatedAt, o.Title, o.ExternalNumber, o.Mode, o.Status, o.DueDate,
                Project = o.Project.Name,
                Branch = o.Project.Branch.Name,
                Total = o.Deliverables.Count,
                Approved = o.Deliverables.Count(d => d.State == DeliverableState.Approved),
                Late = o.Status == StudyStatus.Open
                    ? o.Deliverables.Count(d => d.State != DeliverableState.Approved && d.DueDate != null && d.DueDate < today) : 0,
                Rounds = o.Submissions.Count,
                CreatedBy = o.CreatedByUser.FullName
            });

            ReportColumnDto[] columns =
            [
                Col("الرقم"), Col("العنوان"), Col("المشروع"), Col("الفرع", print: false), Col("الحالة"), Col("الترتيب", print: false),
                Col("المخرجات", Ct.Number), Col("المعتمدة", Ct.Number), Col("النسبة %", Ct.Number), Col("متأخرة", Ct.Number),
                Col("جولات الإرسال", Ct.Number, print: false), Col("الموعد", Ct.Date), Col("فتحها", print: false), Col("تاريخ الفتح", Ct.Date)
            ];
            return QueryAsync(rows, run, columns, x => new object?[]
            {
                StudyRules.Number(x.Id, x.CreatedAt), x.Title, x.Project, x.Branch, StudyRules.StatusAr(x.Status), StudyRules.ModeAr(x.Mode),
                x.Total, x.Approved, x.Total == 0 ? 0 : (int)Math.Floor(100.0 * x.Approved / x.Total), x.Late,
                x.Rounds, x.DueDate?.Date, x.CreatedBy, x.CreatedAt.Date
            }, ct);
        }
    }
}
