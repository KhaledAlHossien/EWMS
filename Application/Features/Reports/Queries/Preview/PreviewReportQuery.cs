using Application.DTOs.Request;
using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.Preview
{
    /// <summary>أول الصفوف مع العدد الكلي — لا تُسجَّل</summary>
    public record PreviewReportQuery(string Key, ReportRunRequestDto Request) : IRequest<ReportPreviewDto>;
}
