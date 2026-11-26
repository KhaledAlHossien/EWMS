using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.Preview
{
    public class PreviewReportQueryHandler : IRequestHandler<PreviewReportQuery, ReportPreviewDto>
    {
        private readonly ReportSupport _support;

        public PreviewReportQueryHandler(ReportSupport support)
        {
            _support = support;
        }

        public async Task<ReportPreviewDto> Handle(PreviewReportQuery request, CancellationToken ct)
        {
            var (_, _, table, text) = await _support.RunAsync(request.Key, request.Request, ReportRules.PreviewRows, false, ct);
            return new ReportPreviewDto { FiltersText = text, Table = table };
        }
    }
}
