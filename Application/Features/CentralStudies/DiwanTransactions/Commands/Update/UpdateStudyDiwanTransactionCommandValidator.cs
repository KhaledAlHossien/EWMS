using FluentValidation;

namespace Application.Features.CentralStudies.DiwanTransactions.Commands.Update
{
    public class UpdateStudyDiwanTransactionCommandValidator : AbstractValidator<UpdateStudyDiwanTransactionCommand>
    {
        public UpdateStudyDiwanTransactionCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyDiwanTransactionFormValidator());
    }
}
