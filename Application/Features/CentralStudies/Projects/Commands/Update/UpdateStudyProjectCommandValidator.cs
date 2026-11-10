using FluentValidation;

namespace Application.Features.CentralStudies.Projects.Commands.Update
{
    public class UpdateStudyProjectCommandValidator : AbstractValidator<UpdateStudyProjectCommand>
    {
        public UpdateStudyProjectCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyProjectFormValidator());
    }
}
