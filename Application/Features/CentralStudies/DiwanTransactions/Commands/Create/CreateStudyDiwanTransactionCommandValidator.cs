using FluentValidation;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Create
{
    public class CreateStudyDiwanTransactionCommandValidator : AbstractValidator<CreateStudyDiwanTransactionCommand>
    {
        public CreateStudyDiwanTransactionCommandValidator()
        {
            RuleFor(x => x.Dto.ResponseId).GreaterThan(0).WithMessage("حدد إشعار الرد");
            RuleFor(x => x.Dto).SetValidator(new StudyDiwanTransactionFormValidator());
        }
    }
}
