using FluentValidation;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Update
{
    public class UpdateMaintenanceRequestStatusCommandValidator : AbstractValidator<UpdateMaintenanceRequestStatusCommand>
    {
        public UpdateMaintenanceRequestStatusCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new MaintenanceRequestStatusRequestDtoValidator());
    }
}
