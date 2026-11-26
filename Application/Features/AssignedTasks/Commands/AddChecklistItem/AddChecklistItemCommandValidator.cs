using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.AddChecklistItem
{
    public class AddChecklistItemCommandValidator : AbstractValidator<AddChecklistItemCommand>
    {
        public AddChecklistItemCommandValidator() =>
            RuleFor(x => x.Text).Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("اكتب نص البند")
                .MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
    }
}
