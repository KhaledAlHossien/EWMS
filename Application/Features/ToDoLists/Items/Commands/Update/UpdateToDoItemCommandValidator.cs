using FluentValidation;

namespace Application.Features.ToDoLists.Items.Commands.Update
{
    public class UpdateToDoItemCommandValidator : AbstractValidator<UpdateToDoItemCommand>
    {
        public UpdateToDoItemCommandValidator()
        {
            RuleFor(x => x.Dto).Must(d => d.Title != null || d.IsDone != null || d.Note != null || d.IsImportant != null
                    || d.Color != null || d.DueDate != null || d.ClearDueDate || d.Repeat != null).WithMessage("لا يوجد ما يُعدَّل");
            RuleFor(x => x.Dto.Color).Must(ToDoRules.IsColor).WithMessage("لون البند غير صحيح");
            RuleFor(x => x.Dto.Title).NotEmpty().When(x => x.Dto.Title != null).WithMessage("عنوان البند مطلوب")
                .MaximumLength(ToDoRules.MaxTitleLength).WithMessage($"عنوان البند لا يتجاوز {ToDoRules.MaxTitleLength} حرف");
            RuleFor(x => x.Dto.Note).MaximumLength(ToDoRules.MaxNoteLength).WithMessage($"الملاحظة لا تتجاوز {ToDoRules.MaxNoteLength} حرف");
        }
    }
}
