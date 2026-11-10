using FluentValidation;

namespace Application.Features.CentralStudies.Projects.Commands.AddMember
{
    public class AddStudyProjectMemberCommandValidator : AbstractValidator<AddStudyProjectMemberCommand>
    {
        public AddStudyProjectMemberCommandValidator()
        {
            RuleFor(x => x.Dto.UserId).GreaterThan(0).WithMessage("اختر الموظف");
            RuleFor(x => x.Dto.RoleTitle).MaximumLength(100).WithMessage("الدور لا يتجاوز 100 حرف");
        }
    }
}
