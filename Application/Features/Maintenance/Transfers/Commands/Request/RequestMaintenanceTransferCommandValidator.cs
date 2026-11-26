using FluentValidation;

namespace Application.Features.Maintenance.Transfers.Commands.Request
{
    public class RequestMaintenanceTransferCommandValidator : AbstractValidator<RequestMaintenanceTransferCommand>
    {
        public RequestMaintenanceTransferCommandValidator()
        {
            RuleFor(x => x.Dto.Reason)
                .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("اكتب سبب طلب التحويل")
                .MaximumLength(500).WithMessage("السبب لا يتجاوز 500 حرف");
            RuleFor(x => x.Dto.SuggestedUserId).GreaterThan(0).When(x => x.Dto.SuggestedUserId != null)
                .WithMessage("الزميل المقترح غير صحيح");
        }
    }
}
