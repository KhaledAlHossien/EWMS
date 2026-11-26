using Domain.Enums;
using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.ChangeStatus
{
    public class ChangeAssignedTaskStatusCommandValidator : AbstractValidator<ChangeAssignedTaskStatusCommand>
    {
        public ChangeAssignedTaskStatusCommandValidator()
        {
            RuleFor(x => x.Status).Must(s => Enum.IsDefined(typeof(AssignedTaskStatus), s)).WithMessage("الحالة غير صحيحة");
            RuleFor(x => x.Note).MaximumLength(500).WithMessage("السبب لا يتجاوز 500 حرف");
            RuleFor(x => x.ExpectedStatus!.Value).Must(s => Enum.IsDefined(typeof(AssignedTaskStatus), s))
                .When(x => x.ExpectedStatus != null).WithMessage("الحالة غير صحيحة");
        }
    }
}
