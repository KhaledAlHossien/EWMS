using FluentValidation;

namespace Application.Features.Vacations.Commands.Cancel
{
    public class CancelVacationCommandValidator : AbstractValidator<CancelVacationCommand>
    {
        public CancelVacationCommandValidator()
        {
            RuleFor(x => x.VacationId)
                .GreaterThan(0).WithMessage("رقم الإجازة غير صحيح");
        }
    }
}
