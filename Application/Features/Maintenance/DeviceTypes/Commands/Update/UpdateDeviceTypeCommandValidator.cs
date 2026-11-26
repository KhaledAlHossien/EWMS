using FluentValidation;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Update
{
    public class UpdateDeviceTypeCommandValidator : AbstractValidator<UpdateDeviceTypeCommand>
    {
        public UpdateDeviceTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceTypeRequestDtoValidator());
    }
}
