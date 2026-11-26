using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Maintenance.Requests
{
    public class SaveMaintenanceRequestDtoValidator : AbstractValidator<SaveMaintenanceRequestDto>
    {
        public SaveMaintenanceRequestDtoValidator()
        {
            // العميل الموظف يُؤخذ اسمه من حسابه؛ الاسم مطلوب للعميل الخارجي فقط
            RuleFor(x => x.ClientName)
                .NotEmpty().WithMessage("اسم العميل مطلوب").When(x => x.ClientUserId == null)
                .MaximumLength(200).WithMessage("اسم العميل لا يتجاوز 200 حرف");

            RuleFor(x => x.ClientPhone)
                .MaximumLength(30).WithMessage("رقم الهاتف لا يتجاوز 30 خانة")
                .Must(MaintenanceRules.IsValidPhone)
                .WithMessage("رقم الهاتف غير صحيح — مثال: 0933123456 أو 0112345678 أو ‎+963933123456");

            RuleFor(x => x.DeviceMaintenanceId).GreaterThan(0).WithMessage("يجب اختيار الجهاز");
            RuleFor(x => x.AssigneeId).GreaterThan(0).When(x => x.AssigneeId != null).WithMessage("الفني المسؤول غير صحيح");
            RuleFor(x => x.DamageTypeId).GreaterThan(0).WithMessage("يجب اختيار نوع العطل");
            RuleFor(x => x.MaintenanceRequestStatusId).GreaterThan(0).WithMessage("يجب اختيار حالة الطلب");

            RuleFor(x => x.Accessories).MaximumLength(500).WithMessage("الملحقات لا تتجاوز 500 حرف");
            RuleFor(x => x.Description).MaximumLength(2000).WithMessage("الوصف لا يتجاوز 2000 حرف");

        }
    }
}
