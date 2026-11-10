using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Responses
{

    public class StudyResponseFormValidator : AbstractValidator<StudyResponseFormDto>
    {
        public const int MaxItems = 200;

        public StudyResponseFormValidator()
        {
            RuleFor(x => x.ResponseDate).NotEmpty().WithMessage("تاريخ الرد مطلوب");
            RuleFor(x => x.Recommendation).Must(v => Enum.IsDefined(typeof(StudyRecommendation), v)).WithMessage("حدد التوصية الفنية النهائية");
            RuleFor(x => x.SiteArea).GreaterThanOrEqualTo(0).When(x => x.SiteArea != null).WithMessage("مساحة الموقع لا تكون سالبة");
            RuleFor(x => x.InitialCost).GreaterThanOrEqualTo(0).When(x => x.InitialCost != null).WithMessage("التكلفة لا تكون سالبة");
            RuleFor(x => x.ProposedManagerUserId).GreaterThan(0).When(x => x.ProposedManagerUserId != null).WithMessage("مدير المشروع المقترح غير صالح");

            RuleFor(x => x.Buildings).Must(b => b.Count <= 2).WithMessage("بيانات المباني سطران على الأكثر (إعادة تأهيل وإنشاء جديد)")
                .Must(b => b.Select(x => x.BuildingType).Distinct().Count() == b.Count).WithMessage("نوع البناء مرة واحدة فقط");
            RuleForEach(x => x.Buildings).ChildRules(b =>
            {
                b.RuleFor(x => x.BuildingType).Must(v => Enum.IsDefined(typeof(StudyBuildingType), v)).WithMessage("نوع البناء غير صالح");
                b.RuleFor(x => x.BuildingCount).GreaterThanOrEqualTo(0).WithMessage("عدد المباني لا يكون سالباً");
                b.RuleFor(x => x.BuildingsArea).GreaterThanOrEqualTo(0).WithMessage("المساحة لا تكون سالبة");
            });

            RuleFor(x => x.CostItems).Must(c => c.Count <= MaxItems).WithMessage($"بنود التكلفة {MaxItems} على الأكثر");
            RuleForEach(x => x.CostItems).ChildRules(c =>
            {
                c.RuleFor(x => x.Description).NotEmpty().WithMessage("وصف البند مطلوب").MaximumLength(500).WithMessage("وصف البند لا يتجاوز 500 حرف");
                c.RuleFor(x => x.Unit).NotEmpty().WithMessage("وحدة البند مطلوبة").MaximumLength(30).WithMessage("الوحدة لا تتجاوز 30 حرفاً");
                c.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("الكمية لا تكون سالبة");
                c.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("سعر الوحدة لا يكون سالباً");
            });

        }
    }
}
