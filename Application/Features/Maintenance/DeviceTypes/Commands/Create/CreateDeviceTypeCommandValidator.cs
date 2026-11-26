using FluentValidation;

namespace Application.Features.Maintenance.DeviceTypes.Commands.Create
{
    public class CreateDeviceTypeCommandValidator : AbstractValidator<CreateDeviceTypeCommand>
    {
        public CreateDeviceTypeCommandValidator() =>
            RuleFor(x => x.Dto).SetValidator(new DeviceTypeRequestDtoValidator());
    }
}
