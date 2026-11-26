using FluentValidation;

namespace Application.Features.AssignedTasks.Commands.Update
{
    public class UpdateAssignedTaskCommandValidator : AbstractValidator<UpdateAssignedTaskCommand>
    {
        public UpdateAssignedTaskCommandValidator()
        {
            RuleFor(x => x.Dto.Title).NotEmpty().WithMessage("عنوان المهمة مطلوب")
                .MaximumLength(200).WithMessage("العنوان لا يتجاوز 200 حرف");
            RuleFor(x => x.Dto.Description).MaximumLength(4000).WithMessage("الوصف لا يتجاوز 4000 حرف");
            RuleFor(x => x.Dto.Priority).InclusiveBetween(1, 4).WithMessage("الأولوية غير صحيحة");
        }
    }
}
