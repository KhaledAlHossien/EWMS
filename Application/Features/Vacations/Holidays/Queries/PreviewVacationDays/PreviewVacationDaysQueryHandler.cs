using Application.DTOs.Response;
using Application.Interfaces;
using MediatR;

namespace Application.Features.Vacations.Holidays.Queries.PreviewVacationDays
{
    public class PreviewVacationDaysQueryHandler : IRequestHandler<PreviewVacationDaysQuery, VacationDaysPreviewDto>
    {
        private readonly IPublicHolidayService _service;

        public PreviewVacationDaysQueryHandler(IPublicHolidayService service)
        {
            _service = service;
        }

        public async Task<VacationDaysPreviewDto> Handle(PreviewVacationDaysQuery request, CancellationToken ct)
        {
            var start = request.Start.Date;
            var end = request.End.Date;
            var holidays = await _service.GetBetweenAsync(start, end);
            var holidayDates = await _service.GetDatesAsync(start, end);

            var fridays = 0;
            for (var d = start; d <= end; d = d.AddDays(1))
                if (d.DayOfWeek == VacationCalendar.WeeklyHoliday) fridays++;

            return new VacationDaysPreviewDto
            {
                CalendarDays = (end - start).Days + 1,
                WorkingDays = VacationCalendar.CountWorkingDays(start, end, holidayDates),
                Fridays = fridays,
                Holidays = holidays.Select(PublicHolidaySupport.ToDto).ToList()
            };
        }
    }
}
