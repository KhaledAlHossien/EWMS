using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Responses
{

    public class StudyResponseFormValidator : AbstractValidator<StudyResponseFormDto>
    {
        public const int MaxItems = 200;
        public const int MaxRepetitions = 1000;
        public const decimal MaxBuildingArea = 9_999_999_999.99m;

        public StudyResponseFormValidator()
        {
            RuleFor(x => x.ResponseDate).NotEmpty().WithMessage("تاريخ الرد مطلوب");
            RuleFor(x => x.Recommendation).Must(v => Enum.IsDefined(typeof(StudyRecommendation), v)).WithMessage("حدد التوصية الفنية النهائية");
            RuleFor(x => x.SiteArea).GreaterThanOrEqualTo(0).When(x => x.SiteArea != null).WithMessage("مساحة الموقع لا تكون سالبة");
            RuleFor(x => x.SiteNote).MaximumLength(1000).WithMessage("ملاحظة الموقع العام لا تتجاوز 1000 حرف");
            RuleFor(x => x.InitialCost).GreaterThanOrEqualTo(0).When(x => x.InitialCost != null).WithMessage("التكلفة لا تكون سالبة");
            RuleFor(x => x.UsdToSypRate).GreaterThan(0).WithMessage("سعر الصرف يجب أن يكون أكبر من صفر")
                .PrecisionScale(18, 6, true).WithMessage("أدخل سعر صرف صالحاً حتى ست خانات عشرية");
            RuleFor(x => x.ProposedManagerUserId).GreaterThan(0).When(x => x.ProposedManagerUserId != null).WithMessage("مدير المشروع المقترح غير صالح");

            RuleFor(x => x.Buildings).Must(b => b.Count <= 3).WithMessage("بيانات المباني ثلاثة أسطر على الأكثر (إنشاء جديد، إعادة تأهيل، إعادة تأهيل جزئي)")
                .Must(b => b.Select(x => x.BuildingType).Distinct().Count() == b.Count).WithMessage("نوع البناء مرة واحدة فقط");
            RuleForEach(x => x.Buildings).ChildRules(b =>
            {
                b.RuleFor(x => x.BuildingType).Must(v => Enum.IsDefined(typeof(StudyBuildingType), v)).WithMessage("نوع البناء غير صالح");
                b.RuleFor(x => x.BuildingCount).InclusiveBetween(1, MaxItems).WithMessage("عدد المباني لكل نوع بين 1 و200");
                b.RuleFor(x => x.BuildingsArea).InclusiveBetween(0, MaxBuildingArea).WithMessage("المساحة الإجمالية للمباني خارج النطاق");
                b.RuleFor(x => x.Details).Must((building, rows) => rows != null && rows.Count == building.BuildingCount)
                    .WithMessage("يجب أن يطابق عدد صفوف التفاصيل عدد المباني: أدخل الرمز والوظيفة والمساحة لكل مبنى");
                b.RuleForEach(x => x.Details).NotNull().WithMessage("تفاصيل المبنى مطلوبة").ChildRules(d =>
                {
                    d.RuleFor(x => x.Code).NotEmpty().WithMessage("رمز المبنى مطلوب")
                        .MaximumLength(50).WithMessage("رمز المبنى لا يتجاوز 50 حرفاً");
                    d.RuleFor(x => x.Function).NotEmpty().WithMessage("وظيفة المبنى مطلوبة")
                        .MaximumLength(200).WithMessage("وظيفة المبنى لا تتجاوز 200 حرف");
                    d.RuleFor(x => x.Area).GreaterThan(0).WithMessage("مساحة المبنى يجب أن تكون أكبر من صفر")
                        .PrecisionScale(12, 2, true).WithMessage("أدخل مساحة صالحة بخانتين عشريتين على الأكثر");
                    d.RuleFor(x => x.Repetitions).InclusiveBetween(1, MaxRepetitions)
                        .WithMessage($"عدد تكرارات المبنى بين 1 و{MaxRepetitions}");
                    d.RuleFor(x => x.Note).MaximumLength(500).WithMessage("ملاحظة المبنى لا تتجاوز 500 حرف");
                });
            });

            RuleFor(x => x.CostItems).Must(c => c.Count <= MaxItems).WithMessage($"بنود التكلفة {MaxItems} على الأكثر");
            RuleForEach(x => x.CostItems).ChildRules(c =>
            {
                c.RuleFor(x => x.Description).NotEmpty().WithMessage("وصف البند مطلوب").MaximumLength(500).WithMessage("وصف البند لا يتجاوز 500 حرف");
                c.RuleFor(x => x.Unit).NotEmpty().WithMessage("وحدة البند مطلوبة").MaximumLength(30).WithMessage("الوحدة لا تتجاوز 30 حرفاً");
                c.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0).WithMessage("الكمية لا تكون سالبة");
                c.RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("سعر الوحدة لا يكون سالباً");
                c.RuleFor(x => x.Repetitions).InclusiveBetween(1, MaxRepetitions).WithMessage($"تكرار البند بين 1 و{MaxRepetitions}");
            });

            // الشكل الموحّد: بند تكلفة لكل مبنى بالترتيب (StudyResponseBuildings)
            RuleFor(x => x).Custom((dto, ctx) =>
            {
                if (dto.Buildings.All(b => b.Details != null) && StudyResponseBuildings.Problem(dto) is { } problem)
                    ctx.AddFailure(nameof(StudyResponseFormDto.CostItems), problem);
            });

        }
    }
}
