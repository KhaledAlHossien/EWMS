using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Create
{
    public class CreateStudyBranchCommandValidator : AbstractValidator<CreateStudyBranchCommand>
    {
        public CreateStudyBranchCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyBranchFormValidator());
    }
}
