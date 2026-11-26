using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.Export
{
    public record ExportReportQuery(string Key, ReportRunRequestDto Request) : IRequest<ReportFileDto>;
}
