using Application.DTOs.Response;
using MediatR;

namespace Application.Features.Vacations.Holidays.Queries.PreviewVacationDays
{
    /// <summary>معاينة مدة إجازة قبل تقديمها: أيام العمل بعد استثناء الجمعة والعطل الرسمية</summary>
    public record PreviewVacationDaysQuery(DateTime Start, DateTime End) : IRequest<VacationDaysPreviewDto>;
}
