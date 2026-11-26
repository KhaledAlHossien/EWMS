using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.DeviceInventory
{
    public class DeviceRequestValidator : AbstractValidator<DeviceRequestDto>
    {
        public DeviceRequestValidator()
        {
            RuleFor(x => x.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("اسم الجهاز مطلوب")
                .MaximumLength(100).WithMessage("اسم الجهاز لا يتجاوز 100 حرف");
            RuleFor(x => x.Model).MaximumLength(100).WithMessage("الموديل لا يتجاوز 100 حرف");
            RuleFor(x => x.Description).MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
            RuleFor(x => x.Category).MaximumLength(50).WithMessage("الفئة لا تتجاوز 50 حرفاً");
            RuleFor(x => x.Manufacturer).MaximumLength(100).WithMessage("الشركة المصنّعة لا تتجاوز 100 حرف");
        }
    }
}
