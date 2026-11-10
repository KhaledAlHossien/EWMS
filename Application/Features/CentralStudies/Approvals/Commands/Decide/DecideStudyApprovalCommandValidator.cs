using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Approvals.Commands.Decide
{
    public class DecideStudyApprovalCommandValidator : AbstractValidator<DecideStudyApprovalCommand>
    {
        public DecideStudyApprovalCommandValidator()
        {
            RuleFor(x => x.Dto.Decision)
                .Must(d => d is (int)StudyDecision.Approved or (int)StudyDecision.ApprovedWithNotes or (int)StudyDecision.Rejected)
                .WithMessage("القرار: معتمد أو معتمد مع ملاحظات أو مرفوض");
            RuleFor(x => x.Dto.Note).NotEmpty()
                .When(x => x.Dto.Decision is (int)StudyDecision.ApprovedWithNotes or (int)StudyDecision.Rejected)
                .WithMessage("اكتب الملاحظات أو سبب الرفض");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }
}
