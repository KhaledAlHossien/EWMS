using FluentValidation;

namespace Application.Features.CentralStudies.Projects.Commands.Cancel
{
    public class CancelStudyProjectCommandValidator : AbstractValidator<CancelStudyProjectCommand>
    {
        public CancelStudyProjectCommandValidator() =>
            RuleFor(x => x.Reason).NotEmpty().WithMessage("اذكر سبب الإلغاء").MaximumLength(1000).WithMessage("السبب لا يتجاوز 1000 حرف");
    }
}
