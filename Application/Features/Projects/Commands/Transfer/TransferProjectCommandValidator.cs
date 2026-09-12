using FluentValidation;

namespace Application.Features.Projects.Commands.Transfer
{
    public class TransferProjectCommandValidator : AbstractValidator<TransferProjectCommand>
    {
        public TransferProjectCommandValidator()
        {
            RuleFor(x => x.ProjectId).GreaterThan(0);
            RuleFor(x => x.TransferDto.ToDepartmentId)
                .GreaterThan(0).WithMessage("القسم الهدف مطلوب");
        }
    }
}
