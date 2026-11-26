using Application.DTOs.Request;
using FluentValidation;

namespace Application.Features.ToDoLists
{
    public class ToDoListRequestDtoValidator : AbstractValidator<ToDoListRequestDto>
    {
        public ToDoListRequestDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("اسم القائمة مطلوب")
                .MaximumLength(200).WithMessage("اسم القائمة لا يتجاوز 200 حرف");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("الوصف لا يتجاوز 2000 حرف");

            RuleFor(x => x.Color)
                .Must(c => string.IsNullOrEmpty(c) || ToDoRules.Colors.Contains(c)).WithMessage("لون القائمة غير صحيح");

            RuleFor(x => x.Icon)
                .MaximumLength(ToDoRules.MaxIconLength).WithMessage("الرمز قصير: رمز تعبيري واحد");
        }
    }
}
