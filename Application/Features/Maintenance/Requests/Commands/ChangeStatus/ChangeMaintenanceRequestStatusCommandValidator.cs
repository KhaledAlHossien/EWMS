using FluentValidation;

namespace Application.Features.Maintenance.Requests.Commands.ChangeStatus
{
    public class ChangeMaintenanceRequestStatusCommandValidator : AbstractValidator<ChangeMaintenanceRequestStatusCommand>
    {
        public ChangeMaintenanceRequestStatusCommandValidator() =>
            RuleFor(x => x.StatusId).GreaterThan(0).WithMessage("يجب اختيار حالة الطلب");
    }
}
