using FluentValidation;

namespace Application.Features.WorkTasks.Commands.Update
{
    public class UpdateWorkTaskCommandValidator : AbstractValidator<UpdateWorkTaskCommand>
    {
        public UpdateWorkTaskCommandValidator()
        {
            RuleFor(x => x.TaskDto.Name)
                .NotEmpty().WithMessage("اسم المهمة مطلوب")
                .MaximumLength(100).WithMessage("اسم المهمة لا يتجاوز 100 حرف");

            RuleFor(x => x.TaskDto.Description)
                .MaximumLength(500).WithMessage("الوصف لا يتجاوز 500 حرف");

            RuleFor(x => x.TaskDto.Icon)
                .MaximumLength(16).WithMessage("الأيقونة طويلة جداً");

            RuleFor(x => x.TaskDto.BranchId)
                .GreaterThan(0).WithMessage("يجب اختيار الفرع");
        }
    }
}
