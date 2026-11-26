using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Reports.Queries.GetLogOptions
{
    /// <summary>أسماء التقارير لفلتر صفحة السجل (تقارير المركز + تصدير مهام اللوحة)</summary>
    public record GetReportLogOptionsQuery : IRequest<List<ReportOptionDto>>;
}
