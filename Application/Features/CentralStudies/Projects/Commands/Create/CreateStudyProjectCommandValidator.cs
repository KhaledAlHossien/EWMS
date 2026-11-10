using FluentValidation;

namespace Application.Features.CentralStudies.Projects.Commands.Create
{
    public class CreateStudyProjectCommandValidator : AbstractValidator<CreateStudyProjectCommand>
    {
        public CreateStudyProjectCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyProjectFormValidator());
            RuleFor(x => x.Dto.StartOrderId).GreaterThan(0).WithMessage("حدد أمر البدء");
        }
    }
}
