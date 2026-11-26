using FluentValidation;

namespace Application.Features.Maintenance.SpareParts.Commands.Receive
{
    public class ReceiveSparePartCommandValidator : AbstractValidator<ReceiveSparePartCommand>
    {
        public ReceiveSparePartCommandValidator()
        {
            RuleFor(x => x.Dto.Quantity)
                .GreaterThan(0).WithMessage("الكمية المستلمة أكبر من صفر")
                .LessThanOrEqualTo(SparePartRules.MaxQuantity).WithMessage("الكمية كبيرة جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("الكمية بخانتين عشريتين على الأكثر");
            RuleFor(x => x.Dto.UnitCost)
                .GreaterThanOrEqualTo(0).WithMessage("سعر الوحدة لا يكون سالباً")
                .LessThanOrEqualTo(SparePartRules.MaxQuantity).WithMessage("السعر كبير جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("السعر بخانتين عشريتين على الأكثر");
            RuleFor(x => x.Dto.Date)
                .Must(d => d == null || d.Value.Date <= DateTime.Today).WithMessage("تاريخ الاستلام لا يكون في المستقبل");
            RuleFor(x => x.Dto.Source).MaximumLength(500).WithMessage("المصدر لا يتجاوز 500 حرف");
        }
    }
}
