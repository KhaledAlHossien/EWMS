using FluentValidation;

namespace Application.Features.Maintenance.SpareParts.Commands.Issue
{
    public class IssueSparePartCommandValidator : AbstractValidator<IssueSparePartCommand>
    {
        public IssueSparePartCommandValidator()
        {
            RuleFor(x => x.Dto.SparePartId).GreaterThan(0).WithMessage("اختر القطعة");
            RuleFor(x => x.Dto.Quantity)
                .GreaterThan(0).WithMessage("الكمية أكبر من صفر")
                .LessThanOrEqualTo(SparePartRules.MaxQuantity).WithMessage("الكمية كبيرة جداً")
                .Must(SparePartRules.TwoDecimals).WithMessage("الكمية بخانتين عشريتين على الأكثر");
        }
    }
}
