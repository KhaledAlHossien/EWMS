using MediatR;

namespace Application.Features.Users.Commands.ChangeMyPassword
{
    /// <summary>
    /// الموظف يغيّر كلمة مروره بنفسه (قرار المستخدم 2026-10-13): يتحقق من الحالية، والجديدة بسياسة PasswordRules
    /// ومختلفة عنها، ثم تُنهى كل جلساته فيدخل بالكلمة الجديدة. حق لكل مستخدم مسجّل (Auth/Password) — كـ Auth/Me.
    /// الكلمة الحالية الخاطئة 400 لا 401 (401 يسجّل خروج الواجهة).
    /// </summary>
    public record ChangeMyPasswordCommand(string CurrentPassword, string NewPassword) : IRequest<string>;
}
