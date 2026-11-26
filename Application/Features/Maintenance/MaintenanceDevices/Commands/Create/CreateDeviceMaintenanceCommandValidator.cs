using FluentValidation;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Create
{
    public class CreateDeviceMaintenanceCommandValidator : AbstractValidator<CreateDeviceMaintenanceCommand>
    {
        public CreateDeviceMaintenanceCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceMaintenanceRequestDtoValidator());
    }
}
