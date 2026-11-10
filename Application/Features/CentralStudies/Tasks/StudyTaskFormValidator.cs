using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Tasks
{
    public class StudyTaskFormValidator : AbstractValidator<StudyTaskFormDto>
    {
        public StudyTaskFormValidator()
        {
            RuleFor(x => x.TargetType).Must(v => Enum.IsDefined(typeof(StudyTaskTarget), v)).WithMessage("حدد جهة الإسناد");
            RuleFor(x => x.DepartmentId).NotNull().When(x => x.TargetType == (int)StudyTaskTarget.Department).WithMessage("اختر القسم");
            RuleFor(x => x.OfficeId).NotNull().When(x => x.TargetType == (int)StudyTaskTarget.Office).WithMessage("اختر المكتب");
            RuleFor(x => x.AssigneeUserId).NotNull().When(x => x.TargetType == (int)StudyTaskTarget.User).WithMessage("اختر الموظف");
            RuleFor(x => x.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب").MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Weight).InclusiveBetween(0, 100).WithMessage("وزن المهمة بين 0 و100");
            RuleFor(x => x.PlannedEndDate).GreaterThanOrEqualTo(x => x.PlannedStartDate)
                .When(x => x.PlannedStartDate != null && x.PlannedEndDate != null).WithMessage("الانتهاء المخطط لا يسبق البدء");
        }
    }
}
