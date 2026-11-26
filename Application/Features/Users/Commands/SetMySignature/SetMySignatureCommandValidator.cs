using FluentValidation;

namespace Application.Features.Users.Commands.SetMySignature
{
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
}
