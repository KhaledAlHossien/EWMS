using Application.DTOs.Request;
using Application.Features.CentralStudies.Responses;
using FluentValidation;

namespace Application.Features.CentralStudies.StartOrders
{

    public class StudyStartOrderFormValidator : AbstractValidator<StudyStartOrderFormDto>
    {
        public StudyStartOrderFormValidator()
        {
            RuleFor(x => x.CompetentDepartmentId).GreaterThan(0).WithMessage("اختر القسم المختص");
            RuleFor(x => x.ProjectManagerUserId).GreaterThan(0).WithMessage("اختر مدير المشروع");
            RuleFor(x => x.OrderDate).NotEmpty().WithMessage("تاريخ الأمر مطلوب");
            RuleFor(x => x.StudyStartDate).NotEmpty().WithMessage("تاريخ بدء الدراسة مطلوب");
            RuleFor(x => x.StudyDeadline).NotEmpty().WithMessage("الحد الأقصى للإنهاء مطلوب")
                .GreaterThanOrEqualTo(x => x.StudyStartDate).WithMessage("الحد الأقصى للإنهاء لا يسبق تاريخ البدء");
            RuleFor(x => x.Engineers).Must(e => e.Count <= 100).WithMessage("المهندسون 100 على الأكثر")
                .Must(e => e.Select(x => x.EngineerUserId).Distinct().Count() == e.Count).WithMessage("المهندس مرة واحدة في الأمر");
            RuleForEach(x => x.Engineers).ChildRules(e =>
            {
                e.RuleFor(x => x.EngineerUserId).GreaterThan(0).WithMessage("اختر المهندس");
                e.RuleFor(x => x.DisciplineId).GreaterThan(0).WithMessage("اختر اختصاص المهندس");
            });
            RuleFor(x => x.Resources).Must(c => c.Count <= StudyResponseFormValidator.MaxItems).WithMessage("الموارد 200 على الأكثر");
            RuleForEach(x => x.Resources).SetValidator(new StudyItemFormValidator());
        }
    }
}
