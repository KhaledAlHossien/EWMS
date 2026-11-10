using FluentValidation;

namespace Application.Features.CentralStudies.Requests.Commands.Create
{
    public class CreateStudyRequestCommandValidator : AbstractValidator<CreateStudyRequestCommand>
    {
        public CreateStudyRequestCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyRequestFormValidator());
    }
}
