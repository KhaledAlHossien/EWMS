using Domain.Enums;
using FluentValidation;

namespace Application.Features.Users.Commands.Update
{
    public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserCommandValidator()
        {
            RuleFor(x => x.UserDto.Kind)
                .Must(k => k is UserKind.Account or UserKind.NoLogin)
                .WithMessage("نوع الشخص: حساب نظام أو موظف بلا دخول");

            RuleFor(x => x.UserDto.FullName)
                .NotEmpty().WithMessage("اسم المستخدم مطلوب")
                .MaximumLength(150).WithMessage("اسم المستخدم طويل جداً");

            When(x => x.UserDto.Kind == UserKind.Account, () =>
            {
                RuleFor(x => x.UserDto.Email)
                    .NotEmpty().WithMessage("البريد الإلكتروني مطلوب")
                    .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة");

                // عند تعيين كلمة مرور جديدة: سياسة PasswordRules (وتحويل موظف بلا دخول إلى حساب يطلبها — في الـ handler)
                RuleFor(x => x.UserDto.Password).Custom((password, context) =>
                {
                    if (Application.Common.PasswordRules.Problem(password, context.InstanceToValidate.UserDto.Email) is { } problem)
                        context.AddFailure(problem);
                }).When(x => !string.IsNullOrWhiteSpace(x.UserDto.Password));

                RuleFor(x => x.UserDto.RoleId).NotNull().GreaterThan(0).WithMessage("الدور مطلوب");
            });

            // الموظف بلا دخول يُعرَف برقمه الذاتي (الحسابات القديمة بلا رقم تبقى كما هي)
            RuleFor(x => x.UserDto.PersonalIdNumber)
                .NotEmpty().WithMessage("الرقم الذاتي مطلوب للموظف بلا دخول")
                .When(x => x.UserDto.Kind == UserKind.NoLogin);

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
