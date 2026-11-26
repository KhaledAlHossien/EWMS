using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.GetLog
{
    public record GetReportLogQuery(ReportLogFilterDto Filter) : IRequest<PagedResultDto<ReportLogDto>>;
}
