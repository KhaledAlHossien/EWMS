using FluentValidation;

namespace Application.Features.CentralStudies.Drawings.Commands.Update
{
    public class UpdateStudyDrawingCommandValidator : AbstractValidator<UpdateStudyDrawingCommand>
    {
        public UpdateStudyDrawingCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyDrawingFormValidator());
    }
}
