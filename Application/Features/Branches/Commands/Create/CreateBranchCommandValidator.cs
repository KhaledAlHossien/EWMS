using FluentValidation;

namespace Application.Features.Branches.Commands.Create
{
    public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
    {
        public CreateBranchCommandValidator()
        {
            RuleFor(x => x.BranchDto.Name)
                .NotEmpty().WithMessage("اسم الفرع مطلوب")
                .MaximumLength(150).WithMessage("اسم الفرع طويل جداً");
        }
    }
}
