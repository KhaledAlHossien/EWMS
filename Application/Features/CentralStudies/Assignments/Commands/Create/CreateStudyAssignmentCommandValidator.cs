using FluentValidation;

namespace Application.Features.CentralStudies.Assignments.Commands.Create
{
    public class CreateStudyAssignmentCommandValidator : AbstractValidator<CreateStudyAssignmentCommand>
    {
        public CreateStudyAssignmentCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyAssignmentFormValidator());
            RuleFor(x => x.Dto.RequestId).GreaterThan(0).WithMessage("حدد طلب تجهيز الدراسة");
        }
    }
}
