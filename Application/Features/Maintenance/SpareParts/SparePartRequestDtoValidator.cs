using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.Maintenance.SpareParts
{
    public class SparePartRequestDtoValidator : AbstractValidator<SparePartRequestDto>
    {
        public SparePartRequestDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم القطعة مطلوب").MaximumLength(200).WithMessage("اسم القطعة لا يتجاوز 200 حرف");
            RuleFor(x => x.PartNumber).MaximumLength(100).WithMessage("رقم القطعة لا يتجاوز 100 حرف");
            RuleFor(x => x.Unit).NotEmpty().WithMessage("وحدة القطعة مطلوبة (قطعة، متر…)").MaximumLength(30).WithMessage("الوحدة لا تتجاوز 30 حرفاً");
            RuleFor(x => x.Description).MaximumLength(1000).WithMessage("الوصف لا يتجاوز 1000 حرف");
            RuleFor(x => x.MinQuantity)
                .InclusiveBetween(0, SparePartRules.MaxQuantity).WithMessage("الحد الأدنى رقم موجب")
                .Must(SparePartRules.TwoDecimals).WithMessage("الحد الأدنى بخانتين عشريتين على الأكثر");
            RuleForEach(x => x.DeviceTypeIds).GreaterThan(0).WithMessage("نوع جهاز غير صحيح");
            RuleForEach(x => x.DeviceCompanyIds).GreaterThan(0).WithMessage("شركة غير صحيحة");
        }
    }
}
