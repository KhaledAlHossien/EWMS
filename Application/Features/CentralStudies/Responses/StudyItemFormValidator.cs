using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.CentralStudies.Responses
{
    public class StudyItemFormValidator : AbstractValidator<StudyItemFormDto>
    {
        public StudyItemFormValidator()
        {
            RuleFor(x => x.Item).NotEmpty().WithMessage("اسم البند مطلوب").MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
            RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("الكمية لا تكون سالبة");
            RuleFor(x => x.Note).MaximumLength(500).WithMessage("الملاحظة لا تتجاوز 500 حرف");
        }
    }
}
