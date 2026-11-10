using FluentValidation;

namespace Application.Features.CentralStudies.Assignments.Commands.Cancel
{
    public class CancelStudyAssignmentCommandValidator : AbstractValidator<CancelStudyAssignmentCommand>
    {
        public CancelStudyAssignmentCommandValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }
}
