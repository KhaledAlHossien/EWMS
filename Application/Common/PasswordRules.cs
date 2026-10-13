namespace Application.Common
{
    /// <summary>
    /// سياسة كلمة المرور (قرار المستخدم 2026-10-13): 8 أحرف على الأقل، فيها حرف ورقم، ولا تساوي البريد ولا جزأه الأول.
    /// تُطبَّق عند تعيين أي كلمة مرور: إنشاء موظف، تعيينها عند التعديل، تغيير الموظف كلمته بنفسه، التسجيل القديم، وأول مدير.
    /// كلمات المرور القائمة لا تُفحص (محفوظة مشفّرة). الواجهة تطابقها في core/utils/password.ts — عدّلهما معاً.
    /// </summary>
    public static class PasswordRules
    {
        public const int MinLength = 8;

        /// <summary>سبب رفض كلمة المرور بالعربية، أو null إن كانت مقبولة</summary>
        public static string? Problem(string? password, string? email = null)
        {
            if (string.IsNullOrEmpty(password)) return "كلمة المرور مطلوبة";
            if (password.Length < MinLength) return $"كلمة المرور {MinLength} أحرف على الأقل";
            if (!password.Any(char.IsLetter)) return "كلمة المرور يجب أن تحتوي حرفاً واحداً على الأقل";
            if (!password.Any(char.IsDigit)) return "كلمة المرور يجب أن تحتوي رقماً واحداً على الأقل";
            var address = email?.Trim() ?? string.Empty;
            if (address.Length > 0
                && (string.Equals(password, address, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(password, address.Split('@')[0], StringComparison.OrdinalIgnoreCase)))
                return "كلمة المرور لا يجوز أن تكون البريد الإلكتروني أو جزأه الأول";
            return null;
        }
    }

    /// <summary>
    /// الحماية من تخمين كلمات المرور (قرار المستخدم 2026-10-13): قفل الحساب 15 دقيقة بعد 5 محاولات خاطئة متتالية،
    /// و20 محاولة في الدقيقة لكل جهاز (عنوان IP) على طلبات الدخول وتغيير كلمة المرور.
    /// </summary>
    public static class LoginRules
    {
        public const int MaxFailedAttempts = 5;
        public const int LockoutMinutes = 15;
        public const int AttemptsPerDevicePerMinute = 20;
        public const string RateLimitPolicy = "login";

        public const string InvalidCredentials = "البريد أو كلمة المرور غير صحيحة";
        public const string TooManyFromDevice = "محاولات كثيرة من هذا الجهاز، انتظر دقيقة ثم أعد المحاولة.";

        public static string LockedMessage(DateTime lockedUntilUtc)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((lockedUntilUtc - DateTime.UtcNow).TotalMinutes));
            var text = minutes switch { 1 => "دقيقة", 2 => "دقيقتين", <= 10 => $"{minutes} دقائق", _ => $"{minutes} دقيقة" };
            return $"أُوقف الدخول إلى هذا الحساب مؤقتاً بسبب محاولات خاطئة متكررة. حاول بعد {text}.";
        }
    }
}
