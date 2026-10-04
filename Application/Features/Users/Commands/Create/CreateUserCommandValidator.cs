using FluentValidation;

namespace Application.Features.Users.Commands.Create
{
    public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
    {
        public CreateUserCommandValidator()
        {
            RuleFor(x => x.UserDto.FullName)
                .NotEmpty().WithMessage("اسم المستخدم مطلوب")
                .MaximumLength(150).WithMessage("اسم المستخدم طويل جداً");

            RuleFor(x => x.UserDto.Email)
                .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة");

            RuleFor(x => x.UserDto.Password)
                .NotEmpty().WithMessage("كلمة المرور مطلوبة")
                .MinimumLength(6).WithMessage("كلمة المرور يجب أن تكون 6 أحرف على الأقل");

            RuleFor(x => x.UserDto.RoleId).GreaterThan(0).WithMessage("الدور مطلوب");

            RuleFor(x => x.UserDto.PersonalIdNumber)
                .Must(v => System.Text.RegularExpressions.Regex.IsMatch(v!.Trim(), "^[A-Za-z0-9-]{1,20}$"))
                .WithMessage("الرقم الذاتي: حروف لاتينية وأرقام فقط، حتى 20 خانة")
                .When(x => !string.IsNullOrWhiteSpace(x.UserDto.PersonalIdNumber));

            RuleFor(x => x.UserDto.PhoneNumber)
                .Must(Application.Common.PhoneRules.IsValid)
                .WithMessage("رقم التواصل غير صحيح: جوال 09XXXXXXXX أو أرضي يبدأ بـ 0 ورمز المحافظة");
        }
    }
}
