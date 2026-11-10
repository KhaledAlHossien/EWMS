using FluentValidation;

namespace Application.Features.CentralStudies.Tasks.Commands.Create
{
    public class CreateStudyTaskCommandValidator : AbstractValidator<CreateStudyTaskCommand>
    {
        public CreateStudyTaskCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyTaskFormValidator());
            RuleFor(x => x.Dto.StageId).GreaterThan(0).WithMessage("حدد المرحلة");
        }
    }
}
