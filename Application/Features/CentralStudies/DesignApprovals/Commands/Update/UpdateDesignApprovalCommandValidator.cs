using FluentValidation;

namespace Application.Features.CentralStudies.DesignApprovals.Commands.Update
{
    public class UpdateDesignApprovalCommandValidator : AbstractValidator<UpdateDesignApprovalCommand>
    {
        public UpdateDesignApprovalCommandValidator() => RuleFor(x => x.Dto).SetValidator(new DesignApprovalFormValidator());
    }
}
