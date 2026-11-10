using FluentValidation;

namespace Application.Features.CentralStudies.Assignments.Commands.Update
{
    public class UpdateStudyAssignmentCommandValidator : AbstractValidator<UpdateStudyAssignmentCommand>
    {
        public UpdateStudyAssignmentCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyAssignmentFormValidator());
    }
}
