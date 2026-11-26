using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Maintenance.DeviceCompanies
{
    public class DeviceCompanyRequestDtoValidator : AbstractValidator<DeviceCompanyRequestDto>
    {
        public DeviceCompanyRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم الشركة المصنعة مطلوب")
                .MaximumLength(100).WithMessage("اسم الشركة لا يتجاوز 100 حرف");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
        }
    }
}
