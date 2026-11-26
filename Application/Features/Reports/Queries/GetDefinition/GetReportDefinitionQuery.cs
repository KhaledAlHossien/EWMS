using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.GetDefinition
{
    public record GetReportDefinitionQuery(string Key) : IRequest<ReportDefinitionDto>;
}
