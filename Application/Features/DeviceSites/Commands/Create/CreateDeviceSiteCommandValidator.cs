using FluentValidation;

namespace Application.Features.DeviceSites.Commands.Create
{
    public class CreateDeviceSiteCommandValidator : AbstractValidator<CreateDeviceSiteCommand>
    {
        private const string IpPattern =
            @"^(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)(\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)){3}$";

        public CreateDeviceSiteCommandValidator()
        {
            RuleFor(x => x.DeviceSiteDto.DeviceId)
                .GreaterThan(0).WithMessage("يجب اختيار جهاز");

            RuleFor(x => x.DeviceSiteDto.SiteId)
                .GreaterThan(0).WithMessage("يجب اختيار موقع");

            RuleFor(x => x.DeviceSiteDto.Ip)
                .NotEmpty().WithMessage("عنوان الـ IP مطلوب")
                .Matches(IpPattern).WithMessage("صيغة عنوان الـ IP غير صحيحة");

            RuleFor(x => x.DeviceSiteDto.SubnetMask)
                .NotEmpty().WithMessage("الـ Subnet Mask مطلوب")
                .Matches(IpPattern).WithMessage("صيغة الـ Subnet Mask غير صحيحة");

            RuleFor(x => x.DeviceSiteDto.UserName)
                .NotEmpty().WithMessage("اسم المستخدم مطلوب")
                .MaximumLength(100).WithMessage("اسم المستخدم لا يتجاوز 100 حرف");

            RuleFor(x => x.DeviceSiteDto.Pass)
                .NotEmpty().WithMessage("كلمة السر مطلوبة")
                .MaximumLength(200).WithMessage("كلمة السر لا تتجاوز 200 حرف");

            RuleFor(x => x.DeviceSiteDto.SN)
                .MaximumLength(100).WithMessage("الرقم التسلسلي لا يتجاوز 100 حرف");

            RuleFor(x => x.DeviceSiteDto.InstallLocation)
                .MaximumLength(300).WithMessage("مكان التركيب لا يتجاوز 300 حرف");

            RuleFor(x => x.DeviceSiteDto.Note)
                .MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف");
        }
    }
}
