using FluentValidation;

namespace Application.Features.CentralStudies.Responses.Commands.Update
{
    public class UpdateStudyResponseCommandValidator : AbstractValidator<UpdateStudyResponseCommand>
    {
        public UpdateStudyResponseCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyResponseFormValidator());
    }
}
