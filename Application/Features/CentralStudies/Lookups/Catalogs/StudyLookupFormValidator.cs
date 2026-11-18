using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.Catalogs
{
    public class StudyLookupFormValidator : AbstractValidator<StudyLookupFormDto>
    {
        public StudyLookupFormValidator(int maxLength = 100, bool requireCode = false)
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("الاسم مطلوب")
                .MaximumLength(maxLength).WithMessage($"الاسم لا يتجاوز {maxLength} حرف");
            if (requireCode)
                RuleFor(x => x.Code).NotEmpty().WithMessage("رمز الجهة مطلوب")
                    .MaximumLength(30).WithMessage("رمز الجهة لا يتجاوز 30 حرفاً");
        }
    }
}
