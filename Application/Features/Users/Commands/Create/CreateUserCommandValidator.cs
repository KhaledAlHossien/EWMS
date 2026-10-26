using Domain.Enums;
using FluentValidation;

namespace Application.Features.Users.Commands.Create
{
    public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
    {
        public CreateUserCommandValidator()
        {
            RuleFor(x => x.UserDto.Kind)
                .Must(k => k is UserKind.Account or UserKind.NoLogin)
                .WithMessage("نوع الشخص: حساب نظام أو موظف بلا دخول (عامل المياومة يُضاف من صفحة الأشخاص في الحضور)");

            RuleFor(x => x.UserDto.FullName)
                .NotEmpty().WithMessage("اسم المستخدم مطلوب")
                .MaximumLength(150).WithMessage("اسم المستخدم طويل جداً");

            // البريد وكلمة المرور والدور لحساب النظام وحده
            When(x => x.UserDto.Kind == UserKind.Account, () =>
            {
                RuleFor(x => x.UserDto.Email)
                    .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                    .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة");

                // سياسة كلمة المرور (Application.Common.PasswordRules): 8 أحرف على الأقل، فيها حرف ورقم، ولا تساوي البريد
                RuleFor(x => x.UserDto.Password).Custom((password, context) =>
                {
                    if (Application.Common.PasswordRules.Problem(password, context.InstanceToValidate.UserDto.Email) is { } problem)
                        context.AddFailure(problem);
                });

                RuleFor(x => x.UserDto.RoleId).NotNull().GreaterThan(0).WithMessage("الدور مطلوب");
            });

            // كل موظف رسمي جديد له رقم ذاتي فريد (قرار المستخدم 2026-10-26)
            RuleFor(x => x.UserDto.PersonalIdNumber)
                .NotEmpty().WithMessage("الرقم الذاتي مطلوب للموظف الرسمي");

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
