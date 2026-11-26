using FluentValidation;

namespace Application.Features.DeviceInventory.Devices.Commands.Update
{
    public class UpdateDeviceCommandValidator : AbstractValidator<UpdateDeviceCommand>
    {
        public UpdateDeviceCommandValidator() => RuleFor(x => x.Dto).SetValidator(new DeviceRequestValidator());
    }
}
