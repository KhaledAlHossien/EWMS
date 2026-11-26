using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Reports.Queries.GetLog
{
    public class GetReportLogQueryHandler : IRequestHandler<GetReportLogQuery, PagedResultDto<ReportLogDto>>
    {
        private readonly IReportLogService _log;
        private readonly ReportSupport _support;

        public GetReportLogQueryHandler(IReportLogService log, ReportSupport support)
        {
            _log = log;
            _support = support;
        }

        public async Task<PagedResultDto<ReportLogDto>> Handle(GetReportLogQuery request, CancellationToken ct)
        {
            var viewer = await _support.CurrentAsync();
            var filter = request.Filter;
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var (items, total) = await _log.SearchAsync(filter, ReportRules.LogBranchOf(viewer), page, pageSize);
            return new PagedResultDto<ReportLogDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
        }
    }
}
