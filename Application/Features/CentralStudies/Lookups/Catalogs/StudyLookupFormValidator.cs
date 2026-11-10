using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.Catalogs
{
    public class StudyLookupFormValidator : AbstractValidator<StudyLookupFormDto>
    {
        public StudyLookupFormValidator() =>
            RuleFor(x => x.Name).NotEmpty().WithMessage("الاسم مطلوب").MaximumLength(100).WithMessage("الاسم لا يتجاوز 100 حرف");
    }
}
