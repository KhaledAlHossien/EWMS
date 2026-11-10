using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.CentralStudies.Responses
{
    /// <summary>جزء التخطيط وإدارة المشاريع: المدة والكادر والموارد اللوجستية (كلها اختيارية)</summary>
    public class StudyResponsePlanningFormValidator : AbstractValidator<StudyResponsePlanningFormDto>
    {
        public StudyResponsePlanningFormValidator()
        {
            RuleFor(x => x.RequiredDays).InclusiveBetween(1, 3650).When(x => x.RequiredDays != null).WithMessage("المدة اللازمة بين 1 و3650 يوماً");
            RuleFor(x => x.StaffEstimates).Must(s => s.Select(x => x.DisciplineId).Distinct().Count() == s.Count)
                .WithMessage("الاختصاص مرة واحدة في الكادر المقترح");
            RuleForEach(x => x.StaffEstimates).ChildRules(s =>
            {
                s.RuleFor(x => x.DisciplineId).GreaterThan(0).WithMessage("اختر الاختصاص");
                s.RuleFor(x => x.StaffCount).InclusiveBetween(0, 1000).WithMessage("العدد بين 0 و1000");
            });

            RuleFor(x => x.LogisticItems).Must(c => c.Count <= StudyResponseFormValidator.MaxItems).WithMessage($"الموارد {StudyResponseFormValidator.MaxItems} على الأكثر");
            RuleForEach(x => x.LogisticItems).SetValidator(new StudyItemFormValidator());
        }
    }
}
