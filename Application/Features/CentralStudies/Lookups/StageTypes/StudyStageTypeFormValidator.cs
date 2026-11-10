using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Lookups.StageTypes
{

    public class StudyStageTypeFormValidator : AbstractValidator<StudyStageTypeFormDto>
    {
        public StudyStageTypeFormValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المرحلة مطلوب").MaximumLength(100).WithMessage("اسم المرحلة لا يتجاوز 100 حرف");
            RuleFor(x => x.DefaultOrder).InclusiveBetween(0, 1000).WithMessage("الترتيب بين 0 و1000");
            RuleFor(x => x.DefaultWeight).InclusiveBetween(0, 100).WithMessage("الوزن بين 0 و100");
            RuleFor(x => x.LinkedDocument).Must(v => Enum.IsDefined(typeof(StudyLinkedDocument), v)).WithMessage("المستند المرتبط غير صالح");
        }
    }
}
