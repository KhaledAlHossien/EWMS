using FluentValidation;

namespace Application.Features.CentralStudies.Responses.Commands.UpdatePlanning
{
    public class UpdateStudyResponsePlanningCommandValidator : AbstractValidator<UpdateStudyResponsePlanningCommand>
    {
        public UpdateStudyResponsePlanningCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyResponsePlanningFormValidator());
    }
}
