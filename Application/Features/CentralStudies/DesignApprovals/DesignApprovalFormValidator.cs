using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.CentralStudies.DesignApprovals
{
    public class DesignApprovalFormValidator : AbstractValidator<DesignApprovalFormDto>
    {
        public DesignApprovalFormValidator()
        {
            RuleFor(x => x.CdaDate).NotEmpty().WithMessage("تاريخ الاعتماد مطلوب");
            RuleFor(x => x.Content).MaximumLength(4000).WithMessage("المحتوى لا يتجاوز 4000 حرف");
            RuleFor(x => x.TechnicalNotes).MaximumLength(4000).WithMessage("الملاحظات الفنية لا تتجاوز 4000 حرف");
        }
    }
}
