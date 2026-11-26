using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Maintenance.DeviceTypes
{
    public class DeviceTypeRequestDtoValidator : AbstractValidator<DeviceTypeRequestDto>
    {
        public DeviceTypeRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم نوع الجهاز مطلوب")
                .MaximumLength(100).WithMessage("اسم نوع الجهاز لا يتجاوز 100 حرف");

        }
    }
}
