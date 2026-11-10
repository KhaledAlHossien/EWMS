using FluentValidation;

namespace Application.Features.CentralStudies.Responses.Commands.Create
{
    public class CreateStudyResponseCommandValidator : AbstractValidator<CreateStudyResponseCommand>
    {
        public CreateStudyResponseCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyResponseFormValidator());
            RuleFor(x => x.Dto.AssignmentId).GreaterThan(0).WithMessage("حدد التكليف");
        }
    }
}
