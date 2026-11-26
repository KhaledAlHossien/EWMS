using FluentValidation;

namespace Application.Features.ToDoLists.Items.Commands.Add
{
    public class AddToDoItemCommandValidator : AbstractValidator<AddToDoItemCommand>
    {
        public AddToDoItemCommandValidator()
        {
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان البند مطلوب")
                .MaximumLength(ToDoRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoRules.MaxTitleLength} حرف");
            RuleFor(x => x.Dto.Note).MaximumLength(ToDoRules.MaxNoteLength).WithMessage($"الملاحظة لا تتجاوز {ToDoRules.MaxNoteLength} حرف");
            RuleFor(x => x.Dto).Must(d => ToDoRules.ParseRepeat(d.Repeat) == null || d.DueDate != null).WithMessage("البند المتكرر يحتاج موعداً");
            RuleFor(x => x.Dto.Color).Must(ToDoRules.IsColor).WithMessage("لون البند غير صحيح");
        }
    }
}
