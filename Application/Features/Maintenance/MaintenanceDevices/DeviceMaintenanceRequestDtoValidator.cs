using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Maintenance.MaintenanceDevices
{
    public class DeviceMaintenanceRequestDtoValidator : AbstractValidator<DeviceMaintenanceRequestDto>
    {
        public DeviceMaintenanceRequestDtoValidator()
        {
            RuleFor(x => x.SerialNumber)
                .NotEmpty().WithMessage("الرقم التسلسلي مطلوب")
                .MaximumLength(100).WithMessage("الرقم التسلسلي لا يتجاوز 100 حرف");

            RuleFor(x => x.Name).MaximumLength(200).WithMessage("اسم الجهاز لا يتجاوز 200 حرف");
            RuleFor(x => x.Model).MaximumLength(100).WithMessage("الموديل لا يتجاوز 100 حرف");
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("الوصف لا يتجاوز 1000 حرف");

            RuleFor(x => x.DeviceTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع الجهاز");
            RuleFor(x => x.DeviceCompanyId).GreaterThan(0).WithMessage("يجب اختيار الشركة المصنعة");
        }
    }
}
