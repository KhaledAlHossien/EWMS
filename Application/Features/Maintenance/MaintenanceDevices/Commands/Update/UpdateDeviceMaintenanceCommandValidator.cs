using FluentValidation;

namespace Application.Features.Maintenance.MaintenanceDevices.Commands.Update
{
    public class UpdateDeviceMaintenanceCommandValidator : AbstractValidator<UpdateDeviceMaintenanceCommand>
    {
        public UpdateDeviceMaintenanceCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceMaintenanceRequestDtoValidator());
    }
}
