using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Refer
{
    public class ReferStudyDiwanTransactionCommandValidator : AbstractValidator<ReferStudyDiwanTransactionCommand>
    {
        public ReferStudyDiwanTransactionCommandValidator()
        {
            RuleFor(x => x.Dto.CentralDecision).Must(v => Enum.IsDefined(typeof(StudyDiwanDecision), v))
                .WithMessage("حدد قرار الديوان المركزي كما في الكتاب: 1 موافقة أو 2 رفض");
            RuleFor(x => x.Dto.Note).Must(n => !string.IsNullOrWhiteSpace(n))
                .When(x => x.Dto.CentralDecision == (int)StudyDiwanDecision.Rejected)
                .WithMessage("اذكر سبب الرفض المركزي ليطّلع عليه مدير المشاريع");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }
}
