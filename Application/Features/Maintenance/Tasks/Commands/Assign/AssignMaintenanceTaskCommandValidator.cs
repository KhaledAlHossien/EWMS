using FluentValidation;

namespace Application.Features.Maintenance.Tasks.Commands.Assign
{
    public class AssignMaintenanceTaskCommandValidator : AbstractValidator<AssignMaintenanceTaskCommand>
    {
        public AssignMaintenanceTaskCommandValidator() =>
            RuleFor(x => x.UserId).GreaterThan(0).WithMessage("يجب اختيار الموظف");
    }
}
