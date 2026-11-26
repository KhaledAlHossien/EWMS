using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.AddComment
{
    public class AddAssignedTaskCommentCommandValidator : AbstractValidator<AddAssignedTaskCommentCommand>
    {
        public AddAssignedTaskCommentCommandValidator()
        {
            RuleFor(x => x.Text).NotEmpty().WithMessage("اكتب نص التعليق")
                .MaximumLength(2000).WithMessage("التعليق لا يتجاوز 2000 حرف");
        }
    }
}
