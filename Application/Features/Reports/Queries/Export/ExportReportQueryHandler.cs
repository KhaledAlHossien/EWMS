using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.Features.Reports.Queries.Export
{
    public class ExportReportQueryHandler : IRequestHandler<ExportReportQuery, ReportFileDto>
    {
        private readonly IReportSpreadsheet _spreadsheet;
        private readonly IReportLogService _log;
        private readonly ReportSupport _support;

        public ExportReportQueryHandler(IReportSpreadsheet spreadsheet, IReportLogService log, ReportSupport support)
        {
            _spreadsheet = spreadsheet;
            _log = log;
            _support = support;
        }

        public async Task<ReportFileDto> Handle(ExportReportQuery request, CancellationToken ct)
        {
            var (report, viewer, table, text) = await _support.RunAsync(request.Key, request.Request, ReportRules.MaxExcelRows, true, ct);
            var now = DateTime.Now;
            var content = _spreadsheet.Build(new ReportSheet(report.Title, text, viewer.User.FullName, now, table));
            await _log.AddAsync(ReportRules.Log(viewer, report.Key, report.Title, ReportFormat.Excel, text, table.Rows.Count));
            return new ReportFileDto { Content = content, FileName = $"{report.Key}-{now:yyyy-MM-dd}.xlsx" };
        }
    }
}
