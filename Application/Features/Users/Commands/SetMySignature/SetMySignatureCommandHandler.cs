using Application.Interfaces;
using MediatR;

namespace Application.Features.Users.Commands.SetMySignature
{
    public class SetMySignatureCommandHandler : IRequestHandler<SetMySignatureCommand, Unit>
    {
        private readonly IUserSignatureService _signatureService;
        private readonly IUserService _userService;
        private readonly IPasswordHasher _passwordHasher;

        public SetMySignatureCommandHandler(IUserSignatureService signatureService, IUserService userService, IPasswordHasher passwordHasher)
        {
            _signatureService = signatureService;
            _userService = userService;
            _passwordHasher = passwordHasher;
        }

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
