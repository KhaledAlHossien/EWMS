using FluentValidation;

namespace Application.Features.CentralStudies.StartOrders.Commands.Update
{
    public class UpdateStudyStartOrderCommandValidator : AbstractValidator<UpdateStudyStartOrderCommand>
    {
        public UpdateStudyStartOrderCommandValidator() => RuleFor(x => x.Dto).SetValidator(new StudyStartOrderFormValidator());
    }
}
