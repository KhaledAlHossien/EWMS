using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Reports.Queries.Print
{
    public class PrintReportQueryHandler : IRequestHandler<PrintReportQuery, ReportPrintDto>
    {
        private readonly IReportLogService _log;
        private readonly ReportSupport _support;

        public PrintReportQueryHandler(IReportLogService log, ReportSupport support)
        {
            _log = log;
            _support = support;
        }

        public async Task<ReportPrintDto> Handle(PrintReportQuery request, CancellationToken ct)
        {
            var (report, viewer, table, text) = await _support.RunAsync(request.Key, request.Request, ReportRules.MaxPrintRows, true, ct);
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
    }
}
