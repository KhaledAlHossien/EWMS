using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.StudyBranches
{
    public class StudyBranchFormValidator : AbstractValidator<StudyBranchFormDto>
    {
        public StudyBranchFormValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم فرع الدراسة مطلوب").MaximumLength(100).WithMessage("اسم فرع الدراسة لا يتجاوز 100 حرف");
            RuleFor(x => x.Code).InclusiveBetween(0, 99).WithMessage("رمز فرع الدراسة رقم بين 0 و99 (يبدأ به كود المشروع)");
            RuleFor(x => x.Track).Must(v => Enum.IsDefined(typeof(StudyTrack), v)).WithMessage("حدد مسار فرع الدراسة: دورة كاملة أو تنسيق ومتابعة");
            RuleFor(x => x.BranchId).GreaterThan(0).When(x => x.BranchId != null).WithMessage("الفرع غير صالح");
        }
    }
}
