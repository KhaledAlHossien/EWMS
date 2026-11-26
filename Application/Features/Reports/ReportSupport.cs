using Application.Common;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;

namespace Application.Features.Reports
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class ReportSupport
    {
        private readonly IReadOnlyList<IReport> _reports;
        private readonly IUserService _users;
        private readonly IUserPermissionService _permissions;

        public ReportSupport(IEnumerable<IReport> reports, IUserService users, IUserPermissionService permissions)
        {
            _reports = reports.ToList();
            _users = users;
            _permissions = permissions;
        }

        public Task<Viewer> CurrentAsync() => Viewer.CurrentAsync(_users, _permissions);

        public static ReportInfoDto Info(IReport r, ReportInfoDto? into = null)
        {
            var dto = into ?? new ReportInfoDto();
            dto.Key = r.Key;
            dto.Title = r.Title;
            dto.Group = r.Group;
            dto.Description = r.Description;
            dto.Landscape = r.Landscape;
            return dto;
        }

        public async Task<(IReport Report, Viewer Viewer)> ResolveAsync(string key)
        {
            var report = _reports.FirstOrDefault(r => r.Key == key) ?? throw new KeyNotFoundException("التقرير غير موجود");
            var viewer = await CurrentAsync();
            if (!viewer.Has(report.Permission))
                throw new UnauthorizedAccessException($"لا تملك صلاحية سحب تقرير «{report.Title}»");
            return (report, viewer);
        }

        public async Task<(IReport Report, Viewer Viewer, ReportTableDto Table, string FiltersText)> RunAsync(
            string key, ReportRunRequestDto request, int limit, bool strict, CancellationToken ct)
        {
            var (report, viewer) = await ResolveAsync(key);
            var filters = await report.GetFiltersAsync(viewer, ct);
            var (values, text) = ReportRules.Parse(filters, request.Filters ?? []);
            var table = await report.RunAsync(new ReportRun(viewer, values, limit, strict), ct);
            if (strict) ReportRules.EnsureWithinLimit(table, limit);
            return (report, viewer, table, text);
        }
    }
}
