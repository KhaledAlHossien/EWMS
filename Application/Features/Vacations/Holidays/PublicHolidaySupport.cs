using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Features.Vacations.Holidays
{
    /// <summary>الدوال المشتركة بين عمليات هذه الميزة</summary>
    public class PublicHolidaySupport
    {
        private readonly IPublicHolidayService _service;

        public PublicHolidaySupport(IPublicHolidayService service)
        {
            _service = service;
        }

        public static PublicHolidayResponseDto ToDto(PublicHoliday h)
        {
            var days = (h.EndDate.Date - h.StartDate.Date).Days + 1;
            return new()
            {
                Id = h.Id,
                StartDate = h.StartDate,
                EndDate = h.EndDate,
                Name = h.Name,
                Days = days,
                Fridays = Enumerable.Range(0, days).Count(i => h.StartDate.AddDays(i).DayOfWeek == VacationCalendar.WeeklyHoliday)
            };
        }

        public static string PeriodText(PublicHoliday h) =>
            h.StartDate.Date == h.EndDate.Date ? $"{h.StartDate:yyyy/MM/dd}" : $"{h.StartDate:yyyy/MM/dd} – {h.EndDate:yyyy/MM/dd}";

        /// <summary>العطل لا تتداخل: عطلة واحدة لكل يوم (رسالة باسم العطلة المتداخلة وفترتها)</summary>
        public async Task EnsureNoOverlapAsync(DateTime start, DateTime end, int? excludeId = null)
        {
            if (await _service.FindOverlapAsync(start, end, excludeId) is { } other)
                throw new InvalidOperationException($"تتداخل مع عطلة «{other.Name}» ({PeriodText(other)})");
        }
    }
}
