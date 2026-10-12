using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Reports
{
    /// <summary>التقارير التي يملك المستخدم صلاحية سحبها</summary>
    public record GetReportCatalogQuery : IRequest<List<ReportInfoDto>>;
    public record GetReportDefinitionQuery(string Key) : IRequest<ReportDefinitionDto>;
    /// <summary>أول الصفوف مع العدد الكلي — لا تُسجَّل</summary>
    public record PreviewReportQuery(string Key, ReportRunRequestDto Request) : IRequest<ReportPreviewDto>;
    public record ExportReportQuery(string Key, ReportRunRequestDto Request) : IRequest<ReportFileDto>;
    public record PrintReportQuery(string Key, ReportRunRequestDto Request) : IRequest<ReportPrintDto>;
    public record GetReportLogQuery(ReportLogFilterDto Filter) : IRequest<PagedResultDto<ReportLogDto>>;
    /// <summary>أسماء التقارير لفلتر صفحة السجل (تقارير المركز + تصدير مهام اللوحة)</summary>
    public record GetReportLogOptionsQuery : IRequest<List<ReportOptionDto>>;

    public class ReportHandler :
        IRequestHandler<GetReportCatalogQuery, List<ReportInfoDto>>,
        IRequestHandler<GetReportDefinitionQuery, ReportDefinitionDto>,
        IRequestHandler<PreviewReportQuery, ReportPreviewDto>,
        IRequestHandler<ExportReportQuery, ReportFileDto>,
        IRequestHandler<PrintReportQuery, ReportPrintDto>,
        IRequestHandler<GetReportLogQuery, PagedResultDto<ReportLogDto>>,
        IRequestHandler<GetReportLogOptionsQuery, List<ReportOptionDto>>
    {
        /// <summary>تصدير لا يمرّ بالمركز لكنه يُسجَّل في السجل نفسه</summary>
        public const string TasksKey = "tasks", TasksTitle = "مهام لوحة المهام";

        private readonly IReadOnlyList<IReport> _reports;
        private readonly IReportSpreadsheet _spreadsheet;
        private readonly IReportLogService _log;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public ReportHandler(IEnumerable<IReport> reports, IReportSpreadsheet spreadsheet, IReportLogService log,
            IUserService users, IUserPermissionService permissions)
        {
            _reports = reports.ToList();
            _spreadsheet = spreadsheet;
            _log = log;
            _users = users;
            _permissions = permissions;
        }

        private Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        private static ReportInfoDto Info(IReport r, ReportInfoDto? into = null)
        {
            var dto = into ?? new ReportInfoDto();
            dto.Key = r.Key;
            dto.Title = r.Title;
            dto.Group = r.Group;
            dto.Description = r.Description;
            dto.Landscape = r.Landscape;
            return dto;
        }

        private async Task<(IReport Report, Viewer Viewer)> ResolveAsync(string key)
        {
            var report = _reports.FirstOrDefault(r => r.Key == key) ?? throw new KeyNotFoundException("التقرير غير موجود");
            var viewer = await CurrentAsync();
            if (!viewer.Has(report.Permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية سحب تقرير «{report.Title}»");
            return (report, viewer);
        }

        private async Task<(IReport Report, Viewer Viewer, ReportTableDto Table, string FiltersText)> RunAsync(
            string key, ReportRunRequestDto request, int limit, bool strict, CancellationToken ct)
        {
            var (report, viewer) = await ResolveAsync(key);
            var filters = await report.GetFiltersAsync(viewer, ct);
            var (values, text) = ReportRules.Parse(filters, request.Filters ?? []);
            var table = await report.RunAsync(new ReportRun(viewer, values, limit, strict), ct);
            if (strict) ReportRules.EnsureWithinLimit(table, limit);
            return (report, viewer, table, text);
        }

        public async Task<List<ReportInfoDto>> Handle(GetReportCatalogQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            return _reports.Where(r => viewer.Has(r.Permission)).Select(r => Info(r)).ToList();
        }

        public async Task<ReportDefinitionDto> Handle(GetReportDefinitionQuery request, CancellationToken ct)
        {
            var (report, viewer) = await ResolveAsync(request.Key);
            var dto = (ReportDefinitionDto)Info(report, new ReportDefinitionDto());
            dto.Filters = await report.GetFiltersAsync(viewer, ct);
            dto.PreviewRows = ReportRules.PreviewRows;
            dto.MaxExcelRows = ReportRules.MaxExcelRows;
            dto.MaxPrintRows = ReportRules.MaxPrintRows;
            return dto;
        }

        public async Task<ReportPreviewDto> Handle(PreviewReportQuery request, CancellationToken ct)
        {
            var (_, _, table, text) = await RunAsync(request.Key, request.Request, ReportRules.PreviewRows, false, ct);
            return new ReportPreviewDto { FiltersText = text, Table = table };
        }

        public async Task<ReportFileDto> Handle(ExportReportQuery request, CancellationToken ct)
        {
            var (report, viewer, table, text) = await RunAsync(request.Key, request.Request, ReportRules.MaxExcelRows, true, ct);
            var now = DateTime.Now;
            var content = _spreadsheet.Build(new ReportSheet(report.Title, text, viewer.User.FullName, now, table));
            await _log.AddAsync(ReportRules.Log(viewer, report.Key, report.Title, ReportFormat.Excel, text, table.Rows.Count));
            return new ReportFileDto { Content = content, FileName = $"{report.Key}-{now:yyyy-MM-dd}.xlsx" };
        }

        public async Task<ReportPrintDto> Handle(PrintReportQuery request, CancellationToken ct)
        {
            var (report, viewer, table, text) = await RunAsync(request.Key, request.Request, ReportRules.MaxPrintRows, true, ct);
            await _log.AddAsync(ReportRules.Log(viewer, report.Key, report.Title, ReportFormat.Print, text, table.Rows.Count));
            return new ReportPrintDto
            {
                Key = report.Key,
                Title = report.Title,
                Landscape = report.Landscape,
                BranchName = viewer.User.Branch?.Name ?? string.Empty,
                FiltersText = text,
                PrintedBy = viewer.User.FullName,
                PrintedAt = DateTime.UtcNow,
                Table = table
            };
        }

        public async Task<PagedResultDto<ReportLogDto>> Handle(GetReportLogQuery request, CancellationToken ct)
        {
            var viewer = await CurrentAsync();
            var filter = request.Filter;
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var (items, total) = await _log.SearchAsync(filter, ReportRules.LogBranchOf(viewer), page, pageSize);
            return new PagedResultDto<ReportLogDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
        }

        public Task<List<ReportOptionDto>> Handle(GetReportLogOptionsQuery request, CancellationToken ct) =>
            Task.FromResult(_reports.Select(r => new ReportOptionDto(r.Key, r.Title, r.Group))
                .Append(new ReportOptionDto(TasksKey, TasksTitle, "المهام")).ToList());
    }
}
