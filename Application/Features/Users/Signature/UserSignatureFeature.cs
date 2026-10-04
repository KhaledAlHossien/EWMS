using Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Application.Features.Users.Signature
{
    /// <summary>توقيعي (Data URL) أو null</summary>
    public record GetMySignatureQuery : IRequest<string?>;

    /// <summary>حفظ توقيعي، أو حذفه إن كانت الصورة فارغة</summary>
    public record SetMySignatureCommand(string? Image) : IRequest<Unit>;

    public class SetMySignatureCommandValidator : AbstractValidator<SetMySignatureCommand>
    {
        // حوالي 300KB بعد ترميز base64 — التوقيع صورة صغيرة
        private const int MaxLength = 400_000;

        public SetMySignatureCommandValidator()
        {
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

        public UserSignatureHandler(IUserSignatureService signatureService, IUserService userService)
        {
            _signatureService = signatureService;
            _userService = userService;
        }

        public Task<string?> Handle(GetMySignatureQuery request, CancellationToken ct) =>
            _signatureService.GetAsync(_userService.UserId);

        public async Task<Unit> Handle(SetMySignatureCommand request, CancellationToken ct)
        {
            var image = string.IsNullOrWhiteSpace(request.Image) ? null : request.Image;
            await _signatureService.SetAsync(_userService.UserId, image);
            return Unit.Value;
        }
    }
}
