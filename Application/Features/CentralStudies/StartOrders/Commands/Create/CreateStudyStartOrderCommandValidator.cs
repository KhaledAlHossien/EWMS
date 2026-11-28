using FluentValidation;

namespace Application.Features.CentralStudies.StartOrders.Commands.Create
{
    public class CreateStudyStartOrderCommandValidator : AbstractValidator<CreateStudyStartOrderCommand>
    {
        public CreateStudyStartOrderCommandValidator()
        {
            RuleFor(x => x.Dto).SetValidator(new StudyStartOrderFormValidator());
            RuleFor(x => x.Dto.DiwanTransactionId).GreaterThan(0).WithMessage("حدد معاملة الديوان المعتمدة بقرار الموافقة");
        }
    }
}
