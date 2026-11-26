using FluentValidation;

namespace Application.Features.Maintenance.Requests.Commands.Assign
{
    public class AssignMaintenanceRequestCommandValidator : AbstractValidator<AssignMaintenanceRequestCommand>
    {
        public AssignMaintenanceRequestCommandValidator() =>
            RuleFor(x => x.UserId).GreaterThan(0).WithMessage("يجب اختيار الموظف");
    }
}
