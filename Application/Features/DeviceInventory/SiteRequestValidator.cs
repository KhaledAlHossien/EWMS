using Application.Common;
using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.DeviceInventory
{
    public class SiteRequestValidator : AbstractValidator<SiteRequestDto>
    {
        public SiteRequestValidator()
        {
            RuleFor(x => x.Name).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("اسم الموقع مطلوب")
                .MaximumLength(100).WithMessage("اسم الموقع لا يتجاوز 100 حرف");
            RuleFor(x => x.Description).MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");
            RuleFor(x => x.ContactName).MaximumLength(100).WithMessage("اسم المسؤول لا يتجاوز 100 حرف");
            RuleFor(x => x.ContactPhone).Must(PhoneRules.IsValid)
                .WithMessage("رقم هاتف المسؤول غير صحيح — مثال: 0933123456 أو 0112345678");
            RuleFor(x => x.ResponsibleParty).MaximumLength(150).WithMessage("الجهة المسؤولة لا تتجاوز 150 حرفاً");
            GeoRules.CoordinatesRules(this, x => x.Latitude, x => x.Longitude);
        }
    }
}
