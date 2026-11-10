using FluentValidation;

namespace Application.Features.CentralStudies.Stages.Commands.Update
{
    public class UpdateStudyStageCommandValidator : AbstractValidator<UpdateStudyStageCommand>
    {
        public UpdateStudyStageCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStageFormValidator());
    }
}
