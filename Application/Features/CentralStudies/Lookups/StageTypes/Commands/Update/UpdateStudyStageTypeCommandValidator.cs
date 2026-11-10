using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Update
{
    public class UpdateStudyStageTypeCommandValidator : AbstractValidator<UpdateStudyStageTypeCommand>
    {
        public UpdateStudyStageTypeCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStageTypeFormValidator());
    }
}
