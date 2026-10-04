using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Features.Users.Signature
{
    /// <summary>توقيعي (Data URL) أو null</summary>
    public record GetMySignatureQuery : IRequest<string?>;

    /// <summary>حفظ توقيعي (نسخة جديدة)، أو إيقافه إن كانت الصورة فارغة — بعد التحقق من كلمة المرور</summary>
    public record SetMySignatureCommand(string? Image, string Password) : IRequest<Unit>;

    public class SetMySignatureCommandValidator : AbstractValidator<SetMySignatureCommand>
    {
        // حوالي 300KB بعد ترميز base64 — التوقيع صورة صغيرة
        private const int MaxLength = 400_000;

        public SetMySignatureCommandValidator()
        {
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("أدخل كلمة المرور لتأكيد تغيير التوقيع");

            RuleFor(x => x.Image)
                .Must(image => image!.StartsWith("data:image/png;base64,") || image.StartsWith("data:image/jpeg;base64,"))
                .WithMessage("صيغة صورة التوقيع غير مدعومة (PNG أو JPEG فقط)")
                .MaximumLength(MaxLength).WithMessage("صورة التوقيع كبيرة جداً (الحد الأقصى 300KB تقريباً)")
                .When(x => !string.IsNullOrWhiteSpace(x.Image));
        }
    }

    public class UserSignatureHandler :
        IRequestHandler<GetMySignatureQuery, string?>,
        IRequestHandler<SetMySignatureCommand, Unit>
    {
        private readonly IUserSignatureService _signatureService;
        private readonly IUserService _userService;
        private readonly IPasswordHasher _passwordHasher;

        public UserSignatureHandler(IUserSignatureService signatureService, IUserService userService, IPasswordHasher passwordHasher)
        {
            _signatureService = signatureService;
            _userService = userService;
            _passwordHasher = passwordHasher;
        }

        public Task<string?> Handle(GetMySignatureQuery request, CancellationToken ct) =>
            _signatureService.GetAsync(_userService.UserId);

        public async Task<Unit> Handle(SetMySignatureCommand request, CancellationToken ct)
        {
            // تأكيد الهوية قبل تغيير التوقيع (قرار المستخدم 2026-10-04). كلمة مرور خاطئة = 400 لا 401
            // (401 يُخرج المستخدم من الواجهة)
            var user = await _userService.GetByIdAsync(_userService.UserId)
                ?? throw new KeyNotFoundException("المستخدم غير موجود");
            if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
                throw new InvalidOperationException("كلمة المرور غير صحيحة");

            var image = string.IsNullOrWhiteSpace(request.Image) ? null : request.Image;
            await _signatureService.SetAsync(_userService.UserId, image);
            return Unit.Value;
        }
    }
}
