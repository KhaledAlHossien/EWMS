using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Tasks.Commands.UpdateProgress
{
    public class UpdateStudyTaskProgressCommandValidator : AbstractValidator<UpdateStudyTaskProgressCommand>
    {
        public UpdateStudyTaskProgressCommandValidator()
        {
            RuleFor(x => x.Dto.Progress).InclusiveBetween(0, 100).WithMessage("نسبة الإنجاز بين 0 و100");
            RuleFor(x => x.Dto.Status).Must(v => Enum.IsDefined(typeof(StudyTaskStatus), v)).WithMessage("حالة المهمة غير صالحة");
        }
    }
}
