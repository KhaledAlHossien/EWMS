using Application.Common;
using Application.DTOs.Request;
using Domain.Entities.CentralStudiesBranch;
using FluentValidation;

namespace Application.Features.CentralStudies.Requests
{

    public class StudyRequestFormValidator : AbstractValidator<StudyRequestFormDto>
    {
        public StudyRequestFormValidator()
        {
            RuleFor(x => x.RequestDate).NotEmpty().WithMessage("تاريخ الطلب مطلوب");
            RuleFor(x => x.Urgency).Must(v => Enum.IsDefined(typeof(StudyUrgency), v)).WithMessage("حدد الاستعجال");
            RuleFor(x => x.WorkType).Must(v => Enum.IsDefined(typeof(StudyWorkType), v)).WithMessage("حدد نوع العمل");
            RuleFor(x => x.MainBeneficiary).NotEmpty().WithMessage("الجهة المستفيدة الرئيسية مطلوبة")
                .MaximumLength(200).WithMessage("الجهة المستفيدة لا تتجاوز 200 حرف");
            RuleFor(x => x.SubBeneficiary).MaximumLength(200).WithMessage("اسم الجهة الفرعية لا يتجاوز 200 حرف");
            RuleFor(x => x.GovernorateCode).Must(Governorates.IsValidCode).WithMessage("اختر المحافظة من القائمة");
            RuleFor(x => x.DiwanRequestNumber).MaximumLength(50).WithMessage("رقم الديوان لا يتجاوز 50 حرفاً");
            RuleFor(x => x.MinistryArchiveNumber).MaximumLength(50).WithMessage("رقم أرشيف الوزارة لا يتجاوز 50 حرفاً");
            RuleFor(x => x.AdminArchiveNumber).MaximumLength(50).WithMessage("رقم أرشيف الإدارة لا يتجاوز 50 حرفاً");
            RuleFor(x => x.BeneficiaryArchiveNumber).MaximumLength(50).WithMessage("رقم أرشيف الجهة لا يتجاوز 50 حرفاً");
            RuleFor(x => x.EntityCode).MaximumLength(30).WithMessage("رمز الجهة لا يتجاوز 30 حرفاً");
            RuleFor(x => x.Area).MaximumLength(100).WithMessage("المنطقة لا تتجاوز 100 حرف");
            RuleFor(x => x.CityOrVillage).MaximumLength(100).WithMessage("المدينة أو القرية لا تتجاوز 100 حرف");
            RuleFor(x => x.PropertyNature).MaximumLength(100).WithMessage("طبيعة العقار لا تتجاوز 100 حرف");
            RuleFor(x => x.FollowerName).MaximumLength(150).WithMessage("اسم المتابع لا يتجاوز 150 حرفاً");
            RuleFor(x => x.FollowerPhone).Must(p => string.IsNullOrWhiteSpace(p) || PhoneRules.IsValid(p))
                .WithMessage("رقم تواصل المتابع غير صحيح (رقم سوري)");
            RuleFor(x => x.Notes).MaximumLength(2000).WithMessage("الملاحظات لا تتجاوز 2000 حرف");
            RuleFor(x => x.SiteId).GreaterThan(0).When(x => x.SiteId != null).WithMessage("الموقع غير صالح");
        }
    }
}
