using Application.Common;
using FluentValidation;

namespace Application.Features.Users.Commands.ChangeMyPassword
{
    public class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
    {
        public ChangeMyPasswordCommandValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("أدخل كلمة المرور الحالية");
            RuleFor(x => x.NewPassword).Custom((password, context) =>
            {
                if (PasswordRules.Problem(password) is { } problem) context.AddFailure(problem);
            });
        }
    }
}
