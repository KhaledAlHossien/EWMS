namespace API.SystemBuild
{
    /// <summary>
    /// يتحقق عند الإقلاع من الإعدادات التي لا يعمل النظام بدونها، ويوقفه برسالة واحدة تذكر كل الناقص.
    /// القيم نفسها في appsettings.Development.json للتطوير، ومن متغيرات البيئة في الإنتاج (docs/DEPLOYMENT.md).
    /// </summary>
    public static class StartupSettings
    {
        public static void EnsureValid(IConfiguration configuration)
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
                problems.Add("نص الاتصال بقاعدة البيانات (ConnectionStrings__DefaultConnection)");

            // HMAC-SHA256 يحتاج مفتاحاً لا يقل عن 32 بايت
            var jwtKey = configuration["JwtSettings:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey) || System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
                problems.Add("مفتاح توقيع الجلسات (JwtSettings__Key): 32 حرفاً على الأقل");

            // مفتاح تشفير كلمات سر الأجهزة: 32 بايت بصيغة Base64. ضياعه يُضيّع كل كلمات السر المحفوظة
            if (!IsBase64Key(configuration["DeviceInventory:PasswordKey"], 32))
                problems.Add("مفتاح تشفير كلمات سر الأجهزة (DeviceInventory__PasswordKey): 32 بايت بصيغة Base64");

            if (problems.Count > 0)
                throw new InvalidOperationException(
                    "إعدادات ناقصة أو غير صالحة، لا يمكن تشغيل الخادم:\n- " + string.Join("\n- ", problems)
                    + "\nراجع docs/DEPLOYMENT.md");
        }

        private static bool IsBase64Key(string? value, int bytes)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            try { return Convert.FromBase64String(value).Length == bytes; }
            catch (FormatException) { return false; }
        }
    }
}
