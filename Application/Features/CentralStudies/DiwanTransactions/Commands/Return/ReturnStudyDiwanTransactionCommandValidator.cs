using FluentValidation;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Return
{
    public class ReturnStudyDiwanTransactionCommandValidator : AbstractValidator<ReturnStudyDiwanTransactionCommand>
    {
        public ReturnStudyDiwanTransactionCommandValidator() =>
            RuleFor(x => x.Dto.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("اذكر ما يجب تصحيحه في المعاملة")
                .MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }
}
