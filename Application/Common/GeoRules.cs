using FluentValidation;

namespace Application.Common
{
    /// <summary>
    /// إحداثيات المناطق والمواقع (خريطة سوريا في لوحة المتابعة):
    /// إجبارية عند الإضافة/التعديل، ويجب أن تقع داخل سوريا تقريباً (مع هامش صغير على الحدود).
    /// </summary>
    public static class GeoRules
    {
        public const double MinLatitude = 32.0, MaxLatitude = 37.6;
        public const double MinLongitude = 35.5, MaxLongitude = 42.6;

        public static void CoordinatesRules<T>(
            AbstractValidator<T> validator,
            System.Linq.Expressions.Expression<Func<T, double?>> latitude,
            System.Linq.Expressions.Expression<Func<T, double?>> longitude)
        {
            validator.RuleFor(latitude)
                .NotNull().WithMessage("خط العرض مطلوب — حدّد النقطة على الخريطة")
                .InclusiveBetween(MinLatitude, MaxLatitude).WithMessage("خط العرض خارج حدود سوريا");

            validator.RuleFor(longitude)
                .NotNull().WithMessage("خط الطول مطلوب — حدّد النقطة على الخريطة")
                .InclusiveBetween(MinLongitude, MaxLongitude).WithMessage("خط الطول خارج حدود سوريا");
        }
    }
}
