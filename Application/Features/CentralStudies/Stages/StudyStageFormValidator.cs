using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Stages
{

    public class StudyStageFormValidator : AbstractValidator<StudyStageFormDto>
    {
        public StudyStageFormValidator()
        {
            RuleFor(x => x.Weight).InclusiveBetween(0, 100).WithMessage("وزن المرحلة بين 0 و100");
            RuleFor(x => x.PlannedEndDate).GreaterThanOrEqualTo(x => x.PlannedStartDate)
                .When(x => x.PlannedStartDate != null && x.PlannedEndDate != null).WithMessage("الانتهاء المخطط لا يسبق البدء");
            RuleFor(x => x.Status).Must(s => s is null or (int)StudyStageStatus.NotStarted or (int)StudyStageStatus.InProgress)
                .WithMessage("الحالة «لم تبدأ» أو «جارية» — الإغلاق بأمر الإغلاق");
        }
    }
}
