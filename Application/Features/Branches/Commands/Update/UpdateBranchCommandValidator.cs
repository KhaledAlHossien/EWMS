using FluentValidation;

namespace Application.Features.Branches.Commands.Update
{
    public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
    {
        public UpdateBranchCommandValidator()
        {
            RuleFor(x => x.BranchDto.Name)
                .NotEmpty().WithMessage("اسم الفرع مطلوب")
                .MaximumLength(150).WithMessage("اسم الفرع طويل جداً");
        }
    }
}
