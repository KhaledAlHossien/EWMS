using FluentValidation;

namespace Application.Features.CentralStudies.StartOrders.Commands.Cancel
{
    public class CancelStudyStartOrderCommandValidator : AbstractValidator<CancelStudyStartOrderCommand>
    {
        public CancelStudyStartOrderCommandValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }
}
