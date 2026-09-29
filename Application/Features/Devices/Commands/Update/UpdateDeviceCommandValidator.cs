using FluentValidation;

namespace Application.Features.Devices.Commands.Update
{
    public class UpdateDeviceCommandValidator : AbstractValidator<UpdateDeviceCommand>
    {
        public UpdateDeviceCommandValidator()
        {
            RuleFor(x => x.DeviceDto.Name)
                .NotEmpty().WithMessage("اسم الجهاز مطلوب")
                .MaximumLength(100).WithMessage("اسم الجهاز لا يتجاوز 100 حرف");

            RuleFor(x => x.DeviceDto.Model)
                .MaximumLength(100).WithMessage("الموديل لا يتجاوز 100 حرف");

            RuleFor(x => x.DeviceDto.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
        }
    }
}
