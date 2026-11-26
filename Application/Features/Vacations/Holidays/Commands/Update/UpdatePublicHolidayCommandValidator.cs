using FluentValidation;

namespace Application.Features.Vacations.Holidays.Commands.Update
{
    public class UpdatePublicHolidayCommandValidator : AbstractValidator<UpdatePublicHolidayCommand>
    {
        public UpdatePublicHolidayCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new PublicHolidayRequestDtoValidator());
    }
}
