using MediatR;

namespace Application.Features.Users.Commands.SetMySignature
{
    /// <summary>حفظ توقيعي (نسخة جديدة)، أو إيقافه إن كانت الصورة فارغة — بعد التحقق من كلمة المرور</summary>
    public record SetMySignatureCommand(string? Image, string Password) : IRequest<Unit>;
}
