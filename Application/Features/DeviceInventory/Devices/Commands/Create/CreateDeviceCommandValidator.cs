using FluentValidation;

namespace Application.Features.DeviceInventory.Devices.Commands.Create
{
    public class CreateDeviceCommandValidator : AbstractValidator<CreateDeviceCommand>
    {
        public CreateDeviceCommandValidator() => RuleFor(x => x.Dto).SetValidator(new DeviceRequestValidator());
    }
}
