using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.Print
{
    public record PrintReportQuery(string Key, ReportRunRequestDto Request) : IRequest<ReportPrintDto>;
}
