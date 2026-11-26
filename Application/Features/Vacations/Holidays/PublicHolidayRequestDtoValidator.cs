using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Vacations.Holidays
{
    public class PublicHolidayRequestDtoValidator : AbstractValidator<PublicHolidayRequestDto>
    {
        public PublicHolidayRequestDtoValidator()
        {
            RuleFor(x => x.StartDate)
                .NotEmpty().WithMessage("تاريخ العطلة مطلوب");

            RuleFor(x => x.EndDate)
                .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("تاريخ نهاية العطلة يجب أن يكون بعد تاريخ بدايتها أو يساويه")
                .Must((x, end) => end == null || (end.Value.Date - x.StartDate.Date).Days < 31)
                .WithMessage("لا تتجاوز العطلة الواحدة 31 يوماً")
                .When(x => x.EndDate != null);

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم العطلة مطلوب")
                .MaximumLength(100).WithMessage("اسم العطلة لا يتجاوز 100 حرف");
        }
    }
}
