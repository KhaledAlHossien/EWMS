using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.GetDefinition
{
    public class GetReportDefinitionQueryHandler : IRequestHandler<GetReportDefinitionQuery, ReportDefinitionDto>
    {
        private readonly ReportSupport _support;

        public GetReportDefinitionQueryHandler(ReportSupport support)
        {
            _support = support;
        }

        public async Task<ReportDefinitionDto> Handle(GetReportDefinitionQuery request, CancellationToken ct)
        {
            var (report, viewer) = await _support.ResolveAsync(request.Key);
            var dto = (ReportDefinitionDto)ReportSupport.Info(report, new ReportDefinitionDto());
            dto.Filters = await report.GetFiltersAsync(viewer, ct);
            dto.PreviewRows = ReportRules.PreviewRows;
            dto.MaxExcelRows = ReportRules.MaxExcelRows;
            dto.MaxPrintRows = ReportRules.MaxPrintRows;
            return dto;
        }
    }
}
