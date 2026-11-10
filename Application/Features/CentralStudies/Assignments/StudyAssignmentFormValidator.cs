using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Assignments
{

    public class StudyAssignmentFormValidator : AbstractValidator<StudyAssignmentFormDto>
    {
        public StudyAssignmentFormValidator()
        {
            RuleFor(x => x.FollowingDepartmentId).GreaterThan(0).WithMessage("اختر القسم المتابع");
            RuleFor(x => x.AssignmentDate).NotEmpty().WithMessage("تاريخ التكليف مطلوب");
            RuleFor(x => x.ProjectCode).MaximumLength(50).WithMessage("كود المشروع لا يتجاوز 50 حرفاً");
            RuleFor(x => x.AllocatedBudget).GreaterThanOrEqualTo(0).When(x => x.AllocatedBudget != null).WithMessage("الميزانية لا تكون سالبة");
            RuleFor(x => x.ScopeSummary).NotEmpty().WithMessage("ملخص نطاق العمل والمتطلبات مطلوب")
                .MaximumLength(4000).WithMessage("ملخص النطاق لا يتجاوز 4000 حرف");
            RuleFor(x => x.ReferralDate).NotEmpty().WithMessage("تاريخ الإحالة الداخلي مطلوب");
            RuleFor(x => x.ResponseDeadline).NotEmpty().WithMessage("الحد الأقصى للرد مطلوب")
                .GreaterThanOrEqualTo(x => x.ReferralDate).WithMessage("الحد الأقصى للرد لا يسبق تاريخ الإحالة");
            RuleFor(x => x.Priority).Must(v => Enum.IsDefined(typeof(StudyPriority), v)).WithMessage("حدد درجة الأهمية");
        }
    }
}
