using FluentValidation;

namespace Application.Features.ToDoLists.Items.Commands.BulkAdd
{
    public class BulkAddToDoItemsCommandValidator : AbstractValidator<BulkAddToDoItemsCommand>
    {
        public BulkAddToDoItemsCommandValidator()
        {
            RuleFor(x => x.Titles).Must(t => t.Any(s => !string.IsNullOrWhiteSpace(s))).WithMessage("لا توجد بنود للإضافة")
                .Must(t => t.Count <= ToDoRules.MaxBulkItems).WithMessage($"الحد الأقصى {ToDoRules.MaxBulkItems} بنداً في المرة الواحدة");
            RuleForEach(x => x.Titles).MaximumLength(ToDoRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoRules.MaxTitleLength} حرف");
        }
    }
}
