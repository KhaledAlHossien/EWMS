using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.GetCatalog
{
    /// <summary>التقارير التي يملك المستخدم صلاحية سحبها</summary>
    public record GetReportCatalogQuery : IRequest<List<ReportInfoDto>>;
}
