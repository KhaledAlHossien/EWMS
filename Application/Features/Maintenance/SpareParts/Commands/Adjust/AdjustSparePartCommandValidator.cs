using FluentValidation;

namespace Application.Features.Maintenance.SpareParts.Commands.Adjust
{
    public class AdjustSparePartCommandValidator : AbstractValidator<AdjustSparePartCommand>
    {
        public AdjustSparePartCommandValidator()
        {
            RuleFor(x => x.Dto.Delta)
                .NotEqual(0).WithMessage("فرق الكمية لا يكون صفراً")
                .InclusiveBetween(-SparePartRules.MaxQuantity, SparePartRules.MaxQuantity).WithMessage("الفرق كبير جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("الكمية بخانتين عشريتين على الأكثر");
            RuleFor(x => x.Dto.Reason).NotEmpty().WithMessage("سبب التسوية مطلوب (جرد، تالف…)").MaximumLength(500).WithMessage("السبب لا يتجاوز 500 حرف");
        }
    }
}
