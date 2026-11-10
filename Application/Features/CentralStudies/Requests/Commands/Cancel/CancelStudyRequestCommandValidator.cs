using FluentValidation;

namespace Application.Features.CentralStudies.Requests.Commands.Cancel
{
    public class CancelStudyRequestCommandValidator : AbstractValidator<CancelStudyRequestCommand>
    {
        public CancelStudyRequestCommandValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }
}
