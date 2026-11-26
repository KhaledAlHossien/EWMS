using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.UpdateChecklistItem
{
    public class UpdateChecklistItemCommandValidator : AbstractValidator<UpdateChecklistItemCommand>
    {
        public UpdateChecklistItemCommandValidator() =>
            RuleFor(x => x.Text).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف")
                .Must(t => t == null || !string.IsNullOrWhiteSpace(t)).WithMessage("نص البند لا يكون فارغاً");
    }
}
