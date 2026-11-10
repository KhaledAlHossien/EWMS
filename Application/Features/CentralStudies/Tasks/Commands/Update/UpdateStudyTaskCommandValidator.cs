using FluentValidation;

namespace Application.Features.CentralStudies.Tasks.Commands.Update
{
    public class UpdateStudyTaskCommandValidator : AbstractValidator<UpdateStudyTaskCommand>
    {
        public UpdateStudyTaskCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyTaskFormValidator());
    }
}
