using Application.DTOs.Request;
using Application.DTOs.Response;
using Application.Interfaces;
using Domain.Entities;
using FluentValidation;
using MediatR;

namespace Application.Features.Vacations.Holidays
{
    public record GetAllPublicHolidaysQuery(int? Year) : IRequest<List<PublicHolidayResponseDto>>;
    public record CreatePublicHolidayCommand(PublicHolidayRequestDto Dto) : IRequest<List<PublicHolidayResponseDto>>;
    public record UpdatePublicHolidayCommand(int Id, PublicHolidayRequestDto Dto) : IRequest<PublicHolidayResponseDto>;
    public record DeletePublicHolidayCommand(int Id) : IRequest<Unit>;

    /// <summary>معاينة مدة إجازة قبل تقديمها: أيام العمل بعد استثناء الجمعة والعطل الرسمية</summary>
    public record PreviewVacationDaysQuery(DateTime Start, DateTime End) : IRequest<VacationDaysPreviewDto>;

    // ════════════════════ التحقق من المدخلات ════════════════════

    public class PublicHolidayRequestDtoValidator : AbstractValidator<PublicHolidayRequestDto>
    {
        public PublicHolidayRequestDtoValidator()
        {
            RuleFor(x => x.Date)
                .NotEmpty().WithMessage("تاريخ العطلة مطلوب");

            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.Date).WithMessage("تاريخ نهاية العطلة يجب أن يكون بعد تاريخ بدايتها أو يساويه")
                .Must((x, end) => end == null || (end.Value.Date - x.Date.Date).Days < 31)
                .WithMessage("لا تتجاوز العطلة الواحدة 31 يوماً")
                .When(x => x.EndDate != null);

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم العطلة مطلوب")
                .MaximumLength(100).WithMessage("اسم العطلة لا يتجاوز 100 حرف");
        }
    }

    public class CreatePublicHolidayCommandValidator : AbstractValidator<CreatePublicHolidayCommand>
    {
        public CreatePublicHolidayCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new PublicHolidayRequestDtoValidator());
    }

    public class UpdatePublicHolidayCommandValidator : AbstractValidator<UpdatePublicHolidayCommand>
    {
        public UpdatePublicHolidayCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new PublicHolidayRequestDtoValidator());
    }

    public class PreviewVacationDaysQueryValidator : AbstractValidator<PreviewVacationDaysQuery>
    {
        public PreviewVacationDaysQueryValidator()
        {
            RuleFor(x => x.End)
                .GreaterThanOrEqualTo(x => x.Start).WithMessage("تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية")
                .Must((x, end) => (end.Date - x.Start.Date).Days < 366).WithMessage("المدة طويلة جداً");
        }
    }

    // ════════════════════ المعالج ════════════════════

    public class PublicHolidayHandler :
        IRequestHandler<GetAllPublicHolidaysQuery, List<PublicHolidayResponseDto>>,
        IRequestHandler<CreatePublicHolidayCommand, List<PublicHolidayResponseDto>>,
        IRequestHandler<UpdatePublicHolidayCommand, PublicHolidayResponseDto>,
        IRequestHandler<DeletePublicHolidayCommand, Unit>,
        IRequestHandler<PreviewVacationDaysQuery, VacationDaysPreviewDto>
    {
        private readonly IPublicHolidayService _service;

        public PublicHolidayHandler(IPublicHolidayService service)
        {
            _service = service;
        }

        private static PublicHolidayResponseDto ToDto(PublicHoliday h) => new()
        {
            Id = h.Id,
            Date = h.Date,
            Name = h.Name,
            IsFriday = h.Date.DayOfWeek == VacationCalendar.WeeklyHoliday
        };

        private async Task<PublicHoliday> LoadAsync(int id) =>
            await _service.GetByIdAsync(id) ?? throw new KeyNotFoundException("العطلة غير موجودة");

        private static string DatesText(IEnumerable<DateTime> dates) =>
            string.Join("، ", dates.OrderBy(d => d).Select(d => d.ToString("yyyy/MM/dd")));

        public async Task<List<PublicHolidayResponseDto>> Handle(GetAllPublicHolidaysQuery request, CancellationToken ct) =>
            (await _service.GetAllAsync(request.Year)).Select(ToDto).ToList();

        public async Task<List<PublicHolidayResponseDto>> Handle(CreatePublicHolidayCommand request, CancellationToken ct)
        {
            var start = request.Dto.Date.Date;
            var end = (request.Dto.EndDate ?? request.Dto.Date).Date;
            var dates = Enumerable.Range(0, (end - start).Days + 1).Select(i => start.AddDays(i)).ToList();

            var existing = await _service.ExistingDatesAsync(dates);
            if (existing.Count > 0)
                throw new InvalidOperationException($"توجد عطلة مسجّلة مسبقاً في: {DatesText(existing)}");

            var name = request.Dto.Name.Trim();
            var holidays = dates.Select(d => new PublicHoliday { Date = d, Name = name }).ToList();
            await _service.AddRangeAsync(holidays);

            return holidays.Select(ToDto).ToList();
        }

        public async Task<PublicHolidayResponseDto> Handle(UpdatePublicHolidayCommand request, CancellationToken ct)
        {
            var holiday = await LoadAsync(request.Id);
            var date = request.Dto.Date.Date;

            if ((await _service.ExistingDatesAsync([date], request.Id)).Count > 0)
                throw new InvalidOperationException($"توجد عطلة أخرى مسجّلة في {date:yyyy/MM/dd}");

            holiday.Date = date;
            holiday.Name = request.Dto.Name.Trim();
            await _service.UpdateAsync(holiday);

            return ToDto(holiday);
        }

        public async Task<Unit> Handle(DeletePublicHolidayCommand request, CancellationToken ct)
        {
            await _service.DeleteAsync(await LoadAsync(request.Id));
            return Unit.Value;
        }

        public async Task<VacationDaysPreviewDto> Handle(PreviewVacationDaysQuery request, CancellationToken ct)
        {
            var start = request.Start.Date;
            var end = request.End.Date;
            var holidays = await _service.GetBetweenAsync(start, end);
            var holidayDates = holidays.Select(h => h.Date).ToHashSet();

            var fridays = 0;
            for (var d = start; d <= end; d = d.AddDays(1))
                if (d.DayOfWeek == VacationCalendar.WeeklyHoliday) fridays++;

            return new VacationDaysPreviewDto
            {
                CalendarDays = (end - start).Days + 1,
                WorkingDays = VacationCalendar.CountWorkingDays(start, end, holidayDates),
                Fridays = fridays,
                Holidays = holidays.Select(ToDto).ToList()
            };
        }
    }
}
