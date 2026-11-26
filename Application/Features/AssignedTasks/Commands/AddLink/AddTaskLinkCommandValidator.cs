using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.AddLink
{
    public class AddTaskLinkCommandValidator : AbstractValidator<AddTaskLinkCommand>
    {
        public AddTaskLinkCommandValidator()
        {
            RuleFor(x => x.Dto.EntityType).NotEmpty().WithMessage("اختر نوع السجل");
            RuleFor(x => x.Dto).Must(d => d.EntityId > 0 || !string.IsNullOrWhiteSpace(d.Reference)).WithMessage("أدخل رقم السجل");
            RuleFor(x => x.Dto.Reference).MaximumLength(40).WithMessage("رقم السجل طويل");
        }
    }
}
