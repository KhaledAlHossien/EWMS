using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Projects
{

    public class StudyProjectFormValidator : AbstractValidator<StudyProjectFormDto>
    {
        public StudyProjectFormValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المشروع مطلوب").MaximumLength(200).WithMessage("اسم المشروع لا يتجاوز 200 حرف");
            RuleFor(x => x.Code).MaximumLength(50).WithMessage("كود المشروع لا يتجاوز 50 حرفاً");
            RuleFor(x => x.PlannedStartDate).NotEmpty().WithMessage("تاريخ البدء المخطط مطلوب");
            RuleFor(x => x.PlannedEndDate).NotEmpty().WithMessage("تاريخ الانتهاء المخطط مطلوب")
                .GreaterThanOrEqualTo(x => x.PlannedStartDate).WithMessage("الانتهاء المخطط لا يسبق البدء");
            RuleFor(x => x.Status).Must(s => s is null or (int)StudyProjectStatus.Running or (int)StudyProjectStatus.Suspended)
                .WithMessage("الحالة جارٍ أو معلّق فقط — الإكمال بالاعتماد والإلغاء بأمر الإلغاء");
        }
    }
}
