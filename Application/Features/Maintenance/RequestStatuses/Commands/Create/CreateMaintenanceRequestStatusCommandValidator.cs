using FluentValidation;

namespace Application.Features.Maintenance.RequestStatuses.Commands.Create
{
    public class CreateMaintenanceRequestStatusCommandValidator : AbstractValidator<CreateMaintenanceRequestStatusCommand>
    {
        public CreateMaintenanceRequestStatusCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new MaintenanceRequestStatusRequestDtoValidator());
    }
}
