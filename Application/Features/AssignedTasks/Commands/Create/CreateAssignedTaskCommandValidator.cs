using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.Create
{
    public class CreateAssignedTaskCommandValidator : AbstractValidator<CreateAssignedTaskCommand>
    {
        public CreateAssignedTaskCommandValidator()
        {
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب")
                .MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Dto.Priority).InclusiveBetween(1, 4).WithMessage("الأولوية غير صحيحة");
            RuleFor(x => x.Dto.TargetId).GreaterThan(0).WithMessage("اختر الجهة المُسندة إليها المهمة");
            RuleFor(x => x.Dto.ChecklistItems).Must(l => l == null || l.Count <= AssignedTaskRules.MaxChecklistItems)
                .WithMessage($"بنود التحقق لا تتجاوز {AssignedTaskRules.MaxChecklistItems} بنداً");
            RuleForEach(x => x.Dto.ChecklistItems).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.ChecklistItems).Must(l => l == null || l.Count <= AssignedTaskRules.MaxChecklistItems)
                .WithMessage($"بنود التحقق لا تتجاوز {AssignedTaskRules.MaxChecklistItems} بنداً");
            RuleForEach(x => x.Dto.ChecklistItems).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.DueDate).Must(d => d == null || d.Value.Date >= DateTime.Today)
                .WithMessage("تاريخ التسليم لا يمكن أن يكون في الماضي");
        }
    }
}
