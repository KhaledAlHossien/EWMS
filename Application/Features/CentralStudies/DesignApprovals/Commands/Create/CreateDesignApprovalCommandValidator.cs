using FluentValidation;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Create
{
    public class CreateDesignApprovalCommandValidator : AbstractValidator<CreateDesignApprovalCommand>
    {
        public CreateDesignApprovalCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new DesignApprovalFormValidator());
            RuleFor(x => x.Dto.VersionId).GreaterThan(0).WithMessage("حدد النسخة المعمارية");
        }
    }
}
