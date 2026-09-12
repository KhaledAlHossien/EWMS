using FluentValidation;

namespace Application.Features.Projects.Commands.AssignUsers
{
    public class AssignProjectUsersCommandValidator : AbstractValidator<AssignProjectUsersCommand>
    {
        public AssignProjectUsersCommandValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0);
            RuleFor(x => x.AssignmentDto.UserIds)
                .NotEmpty().WithMessage("يجب اختيار موظف واحد على الأقل");
        }
    }
}
