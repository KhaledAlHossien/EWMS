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
            RuleFor(x => x.Track).Must(v => Enum.IsDefined(typeof(StudyTrack), v)).WithMessage("حدد مسار المرحلة: دورة كاملة أو تنسيق ومتابعة");
            // مسار التنسيق بلا تكليف ولا رد ولا أمر بدء: المستند الوحيد الذي تُربط به مرحلته هو الطلب
            RuleFor(x => x.LinkedDocument)
                .Must(v => v is (int)StudyLinkedDocument.None or (int)StudyLinkedDocument.Request)
                .When(x => x.Track == (int)StudyTrack.Coordination)
                .WithMessage("مراحل مسار التنسيق تُربط بالطلب فقط أو لا تُربط بمستند");
        }
    }
}
