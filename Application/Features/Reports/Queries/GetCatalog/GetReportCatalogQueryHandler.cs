using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Reports.Queries.GetCatalog
{
    public class GetReportCatalogQueryHandler : IRequestHandler<GetReportCatalogQuery, List<ReportInfoDto>>
    {
        private readonly IReadOnlyList<IReport> _reports;
        private readonly ReportSupport _support;

        public GetReportCatalogQueryHandler(IEnumerable<IReport> reports, ReportSupport support)
        {
            _reports = reports.ToList();
            _support = support;
        }

        public async Task<List<ReportInfoDto>> Handle(GetReportCatalogQuery request, CancellationToken ct)
        {
            var viewer = await _support.CurrentAsync();
            return _reports.Where(r => viewer.Has(r.Permission)).Select(r => ReportSupport.Info(r)).ToList();
        }
    }
}
