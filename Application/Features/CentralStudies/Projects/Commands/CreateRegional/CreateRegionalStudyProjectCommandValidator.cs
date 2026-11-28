using FluentValidation;

namespace Application.Features.CentralStudies.Projects.Commands.CreateRegional
{
    public class CreateRegionalStudyProjectCommandValidator : AbstractValidator<CreateRegionalStudyProjectCommand>
    {
        public CreateRegionalStudyProjectCommandValidator()
        {
            RuleFor(x => x.Dto.RequestId).GreaterThan(0).WithMessage("حدد طلب تجهيز الدراسة");
            RuleFor(x => x.Dto.Name).NotEmpty().WithMessage("اسم المشروع مطلوب").MaximumLength(200).WithMessage("اسم المشروع لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.ReferralDate).NotEmpty().WithMessage("تاريخ الإحالة إلى الفرع مطلوب");
            RuleFor(x => x.Dto.PlannedStartDate).NotEmpty().WithMessage("تاريخ البدء المخطط مطلوب");
            RuleFor(x => x.Dto.PlannedEndDate).NotEmpty().WithMessage("تاريخ الانتهاء المخطط مطلوب")
                .GreaterThanOrEqualTo(x => x.Dto.PlannedStartDate).WithMessage("الانتهاء المخطط لا يسبق البدء");
            RuleFor(x => x.Dto.ManagerUserId).GreaterThan(0).When(x => x.Dto.ManagerUserId != null).WithMessage("مدير المشروع غير صالح");
            RuleFor(x => x.Dto.AdminArchiveNumber).MaximumLength(50).WithMessage("رقم أرشيف ديوان الإدارة لا يتجاوز 50 حرفاً");
        }
    }
}
