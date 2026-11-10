using FluentValidation;

namespace Application.Features.CentralStudies.Requests.Commands.Update
{
    public class UpdateStudyRequestCommandValidator : AbstractValidator<UpdateStudyRequestCommand>
    {
        public UpdateStudyRequestCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyRequestFormValidator());
    }
}
