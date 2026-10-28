namespace Application.Common
{
    /// <summary>
    /// تجديد الجلسة (قرار 2026-10-28): التوكن يعيش JwtSettings:DurationInMinutes (120)، وتجدّده الواجهة تلقائياً
    /// ما دام المستخدم نشطاً، لكن عمر الجلسة الكلي من لحظة الدخول محدود (MaxSessionHours، افتراضي 12 ساعة)
    /// فلا تدوم جلسة للأبد، ثم يدخل من جديد بكلمة المرور. التوكن القديم يعيش دقيقتين بعد التجديد
    /// كي لا تسقط طلبات كانت في الطريق بالتوكن السابق.
    /// </summary>
    public static class SessionRules
    {
        public const int DefaultMaxSessionHours = 12;
        public const int OldTokenGraceSeconds = 120;
        public const string Ended = "انتهت جلستك، سجّل الدخول من جديد";

        /// <summary>هل ما زال التجديد جائزاً؟ (قبل بلوغ الحد الأقصى لعمر الجلسة)</summary>
        public static bool CanRenew(DateTime now, DateTime sessionStartedAt, int maxSessionHours) =>
            now < sessionStartedAt.AddHours(maxSessionHours);

        /// <summary>انتهاء التوكن الجديد: المدة المعتادة، لكن لا يتجاوز نهاية الجلسة القصوى</summary>
        public static DateTime NextExpiry(DateTime now, int durationMinutes, DateTime sessionStartedAt, int maxSessionHours)
        {
            var normal = now.AddMinutes(durationMinutes);
            var hardStop = sessionStartedAt.AddHours(maxSessionHours);
            return normal < hardStop ? normal : hardStop;
        }

        /// <summary>انتهاء التوكن القديم بعد التجديد: يبقى دقيقتين فقط (أو ما بقي له إن كان أقل)</summary>
        public static DateTime OldTokenExpiry(DateTime now, DateTime currentExpiry)
        {
            var grace = now.AddSeconds(OldTokenGraceSeconds);
            return grace < currentExpiry ? grace : currentExpiry;
        }
    }
}
