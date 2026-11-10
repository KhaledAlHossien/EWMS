using FluentValidation;

namespace Application.Features.CentralStudies.Drawings.Commands.Review
{
    public class ReviewStudyDrawingCommandValidator : AbstractValidator<ReviewStudyDrawingCommand>
    {
        public ReviewStudyDrawingCommandValidator()
        {
            RuleFor(x => x.Dto.Note).NotEmpty().When(x => !x.Dto.Approve).WithMessage("اذكر سبب الرفض");
            RuleFor(x => x.Dto.Note).MaximumLength(1000).WithMessage("الملاحظة لا تتجاوز 1000 حرف");
        }
    }
}
