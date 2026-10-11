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

        /// <summary>المسافة بين نقطتين بالمتر (هافرساين) — يكفي لتنبيه المواقع المتقاربة</summary>
        public static double DistanceMeters(double lat1, double lng1, double lat2, double lng2)
        {
            const double EarthRadius = 6_371_000;
            static double Rad(double d) => d * Math.PI / 180;
            var dLat = Rad(lat2 - lat1);
            var dLng = Rad(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * EarthRadius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
        }

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
