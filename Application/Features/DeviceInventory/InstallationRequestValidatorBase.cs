using Application.Common;
using Application.DTOs.Request;
using Domain.Entities;
using FluentValidation;

namespace Application.Features.DeviceInventory
{
    /// <summary>
    /// قواعد التركيب المشتركة. creating: كلمة السر مطلوبة عند الإضافة، وعند التعديل فارغة = تبقى الحالية.
    /// مجرّدة: التسجيل التلقائي للمدققات لا يستطيع تمرير المعامل — الاستخدام عبر الصنفين أدناه.
    /// </summary>
    public abstract class InstallationRequestValidatorBase : AbstractValidator<InstallationRequestDto>
    {
        protected InstallationRequestValidatorBase(bool creating)
        {
            RuleFor(x => x.DeviceId).GreaterThan(0).WithMessage("يجب اختيار جهاز");
            RuleFor(x => x.SiteId).GreaterThan(0).WithMessage("يجب اختيار موقع");
            RuleFor(x => x.Ip).Must(NetworkRules.IsIpv4).WithMessage("عنوان الـ IP غير صحيح (مثال 192.168.1.10، بلا أصفار بادئة)");
            RuleFor(x => x.SubnetMask).Must(NetworkRules.IsSubnetMask)
                .WithMessage("قناع الشبكة غير صحيح — آحاد متصلة ثم أصفار، مثل 255.255.255.0");
            RuleFor(x => x.Gateway)
                .Must(NetworkRules.IsIpv4).WithMessage("عنوان البوابة غير صحيح")
                .Must((dto, gateway) => !NetworkRules.IsIpv4(dto.Ip) || !NetworkRules.IsSubnetMask(dto.SubnetMask) || NetworkRules.SameSubnet(dto.Ip, gateway, dto.SubnetMask))
                .WithMessage("البوابة ليست في شبكة الجهاز (بحسب الـ IP وقناع الشبكة)")
                .When(x => !string.IsNullOrWhiteSpace(x.Gateway));
            RuleFor(x => x.UserName).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("اسم المستخدم مطلوب")
                .MaximumLength(100).WithMessage("اسم المستخدم لا يتجاوز 100 حرف");
            if (creating) RuleFor(x => x.Pass).NotEmpty().WithMessage("كلمة السر مطلوبة");
            RuleFor(x => x.Pass).MaximumLength(200).WithMessage("كلمة السر لا تتجاوز 200 حرف");
            RuleFor(x => x.SN).MaximumLength(100).WithMessage("الرقم التسلسلي لا يتجاوز 100 حرف");
            RuleFor(x => x.InstallLocation).MaximumLength(300).WithMessage("مكان التركيب لا يتجاوز 300 حرف");
            RuleFor(x => x.Note).MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف");
            RuleFor(x => x.MacAddress).Must(NetworkRules.IsMac).WithMessage("عنوان MAC غير صحيح (12 خانة سداسية، مثل AA:BB:CC:DD:EE:FF)");
            RuleFor(x => x.Port).MaximumLength(50).WithMessage("المنفذ لا يتجاوز 50 حرفاً");
            RuleFor(x => x.Vlan).InclusiveBetween(1, 4094).WithMessage("رقم الـ VLAN بين 1 و4094").When(x => x.Vlan != null);
            RuleFor(x => x.Firmware).MaximumLength(100).WithMessage("إصدار البرنامج الثابت لا يتجاوز 100 حرف");
            RuleFor(x => x.InstallDate).Must(d => d == null || d.Value.Date <= DateTime.Today).WithMessage("تاريخ التركيب لا يكون في المستقبل");
            RuleFor(x => x.Status).Must(s => Enum.IsDefined(typeof(InstallationStatus), s)).WithMessage("حالة التركيب غير صحيحة");
            RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("القسم غير صحيح").When(x => x.DepartmentId != null);
        }
    }
}
