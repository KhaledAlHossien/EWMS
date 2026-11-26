using FluentValidation;

namespace Application.Features.Vacations.Holidays.Commands.Create
{
    public class CreatePublicHolidayCommandValidator : AbstractValidator<CreatePublicHolidayCommand>
    {
        public CreatePublicHolidayCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new PublicHolidayRequestDtoValidator());
    }
}
