using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Drawings
{

    public class StudyDrawingFormValidator : AbstractValidator<StudyDrawingFormDto>
    {
        public StudyDrawingFormValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المخطط مطلوب").MaximumLength(200).WithMessage("الاسم لا يتجاوز 200 حرف");
            RuleFor(x => x.DrawingNumber).NotEmpty().WithMessage("رقم المخطط مطلوب").MaximumLength(50).WithMessage("الرقم لا يتجاوز 50 حرفاً");
            RuleFor(x => x.DrawingDate).NotEmpty().WithMessage("تاريخ المخطط مطلوب");
            RuleFor(x => x.Description).MaximumLength(2000).WithMessage("الوصف لا يتجاوز 2000 حرف");
            RuleFor(x => x.Status).Must(s => s is null or (int)DrawingStatus.InPreparation or (int)DrawingStatus.PendingReview)
                .WithMessage("الحالة «قيد الإعداد» أو «بانتظار المراجعة» — الاعتماد بالمراجعة أو CDA");
        }
    }
}
