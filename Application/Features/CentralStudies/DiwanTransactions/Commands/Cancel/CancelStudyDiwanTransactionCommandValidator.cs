using FluentValidation;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Cancel
{
    public class CancelStudyDiwanTransactionCommandValidator : AbstractValidator<CancelStudyDiwanTransactionCommand>
    {
        public CancelStudyDiwanTransactionCommandValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }
}
