using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.CentralStudies.DiwanTransactions
{
    /// <summary>
    /// نموذج معاملة الديوان (الإنشاء والتعديل) — لموظف الديوان: الصادر والرد المركزي بلا قرار (يحدده رئيس الفرع عند الإحالة).
    /// اكتمال الرد المركزي وصورة كتابه يُفحصان عند الإرسال.
    /// </summary>
    public class StudyDiwanTransactionFormValidator : AbstractValidator<StudyDiwanTransactionFormDto>
    {
        public StudyDiwanTransactionFormValidator()
        {
            RuleFor(x => x.DispatchNumber).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("رقم كتاب الصادر إلى الديوان المركزي مطلوب")
                .MaximumLength(50).WithMessage("رقم الصادر لا يتجاوز 50 حرفاً");
            RuleFor(x => x.DispatchDate).NotEmpty().WithMessage("تاريخ الصادر مطلوب");
            RuleFor(x => x.ReplyNumber).MaximumLength(50).WithMessage("رقم الرد المركزي لا يتجاوز 50 حرفاً");
            RuleFor(x => x.ReplyDate).Must((dto, date) => date == null || date.Value.Date >= dto.DispatchDate.Date)
                .WithMessage("تاريخ الرد المركزي لا يسبق تاريخ الصادر");
            RuleFor(x => x.Notes).MaximumLength(1000).WithMessage("الملاحظات لا تتجاوز 1000 حرف");
        }
    }
}
