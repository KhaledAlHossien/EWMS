using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.SaveTemplate
{
    public class SaveTaskTemplateCommandValidator : AbstractValidator<SaveTaskTemplateCommand>
    {
        public SaveTaskTemplateCommandValidator()
        {
            RuleFor(x => x.Dto.Name).NotEmpty().WithMessage("اسم القالب مطلوب").MaximumLength(100).WithMessage("اسم القالب لا يتجاوز 100 حرف");
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب").MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Dto.Priority).InclusiveBetween(1, 4).WithMessage("الأولوية غير صحيحة");
            RuleFor(x => x.Dto.DefaultDueDays).Must(d => d == null || (d >= 0 && d <= 365)).WithMessage("مدة التسليم بين 0 و365 يوماً");
            RuleFor(x => x.Dto.Items).Must(l => l.Count <= AssignedTaskRules.MaxChecklistItems)
                .WithMessage($"بنود التحقق لا تتجاوز {AssignedTaskRules.MaxChecklistItems} بنداً");
            RuleForEach(x => x.Dto.Items).MaximumLength(200).WithMessage("البند لا يتجاوز 200 حرف");
        }
    }
}
