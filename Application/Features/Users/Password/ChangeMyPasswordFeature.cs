using Application.Common;
using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Features.Users.Password
{
    /// <summary>
    /// الموظف يغيّر كلمة مروره بنفسه (قرار المستخدم 2026-10-13): يتحقق من الحالية، والجديدة بسياسة PasswordRules
    /// ومختلفة عنها، ثم تُنهى كل جلساته فيدخل بالكلمة الجديدة. حق لكل مستخدم مسجّل (Auth/Password) — كـ Auth/Me.
    /// الكلمة الحالية الخاطئة 400 لا 401 (401 يسجّل خروج الواجهة).
    /// </summary>
    public record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword) : IRequest<string>;

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

    public class ChangeMyPasswordCommandHandler : IRequestHandler<ChangeMyPasswordCommand, string>
    {
        private readonly IUserService _users;
        private readonly IPasswordHasher _hasher;

        public ChangeMyPasswordCommandHandler(IUserService users, IPasswordHasher hasher)
        {
            _users = users;
            _hasher = hasher;
        }

        public async Task<string> Handle(ChangeMyPasswordCommand request, CancellationToken ct)
        {
            var user = await _users.GetByIdAsync(_users.UserId) ?? throw new UnauthorizedAccessException("المستخدم غير مصادق");
            if (user.PasswordHash == null || !_hasher.Verify(request.CurrentPassword, user.PasswordHash))
                throw new InvalidOperationException("كلمة المرور الحالية غير صحيحة");
            if (PasswordRules.Problem(request.NewPassword, user.Email) is { } problem)
                throw new InvalidOperationException(problem);
            if (_hasher.Verify(request.NewPassword, user.PasswordHash))
                throw new InvalidOperationException("كلمة المرور الجديدة يجب أن تختلف عن الحالية");

            user.PasswordHash = _hasher.Hash(request.NewPassword);
            user.FailedLoginCount = 0;
            user.LockoutUntil = null;
            await _users.UpdateAsync(user);
            // كل الجلسات (ومنها هذه) تنتهي: يدخل من جديد بالكلمة الجديدة، ومن عرف القديمة يخرج
            await _users.RevokeSessionsAsync(user.Id);
            return "تم تغيير كلمة المرور. سجّل الدخول بكلمة المرور الجديدة.";
        }
    }
}
