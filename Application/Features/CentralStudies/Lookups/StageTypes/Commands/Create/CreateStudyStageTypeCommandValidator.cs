using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.StageTypes.Commands.Create
{
    public class CreateStudyStageTypeCommandValidator : AbstractValidator<CreateStudyStageTypeCommand>
    {
        public CreateStudyStageTypeCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStageTypeFormValidator());
    }
}
