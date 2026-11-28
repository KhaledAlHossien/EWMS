using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.StudyBranches.Commands.Update
{
    public class UpdateStudyBranchCommandValidator : AbstractValidator<UpdateStudyBranchCommand>
    {
        public UpdateStudyBranchCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyBranchFormValidator());
    }
}
