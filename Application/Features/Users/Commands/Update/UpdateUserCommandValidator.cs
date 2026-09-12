using FluentValidation;

namespace Application.Features.Users.Commands.Update
{
    public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserCommandValidator()
        {
            RuleFor(x => x.UserDto.FullName)
                .NotEmpty().WithMessage("اسم المستخدم مطلوب")
                .MaximumLength(150).WithMessage("اسم المستخدم طويل جداً");

            RuleFor(x => x.UserDto.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة");

            RuleFor(x => x.UserDto.Password)
                .MinimumLength(6).WithMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل")
                .When(x => !string.IsNullOrWhiteSpace(x.UserDto.Password));

            RuleFor(x => x.UserDto.RoleId).GreaterThan(0).WithMessage("الدور مطلوب");
            RuleFor(x => x.UserDto.DepartmentId).GreaterThan(0).WithMessage("القسم مطلوب");
            RuleFor(x => x.UserDto.BranchId).GreaterThan(0).WithMessage("الفرع مطلوب");
        }
    }
}
