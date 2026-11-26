using FluentValidation;

namespace Application.Features.Vacations.Holidays.Queries.PreviewVacationDays
{
    public class PreviewVacationDaysQueryValidator : AbstractValidator<PreviewVacationDaysQuery>
    {
        public PreviewVacationDaysQueryValidator()
        {
            RuleFor(x => x.End)
                .GreaterThanOrEqualTo(x => x.Start).WithMessage("تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية")
                .Must((x, end) => (end.Date - x.Start.Date).Days < 366).WithMessage("المدة طويلة جداً");
        }
    }
}
