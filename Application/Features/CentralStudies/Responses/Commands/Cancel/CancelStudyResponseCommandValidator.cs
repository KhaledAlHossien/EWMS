using FluentValidation;

namespace Application.Features.CentralStudies.Responses.Commands.Cancel
{
    public class CancelStudyResponseCommandValidator : AbstractValidator<CancelStudyResponseCommand>
    {
        public CancelStudyResponseCommandValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }
}
