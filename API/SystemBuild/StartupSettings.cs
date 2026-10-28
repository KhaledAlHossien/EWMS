namespace API.SystemBuild
{
    /// <summary>
    /// يتحقق عند الإقلاع من الإعدادات التي لا يعمل النظام بدونها، ويوقفه برسالة واحدة تذكر كل الناقص.
    /// القيم نفسها في appsettings.Development.json للتطوير، ومن متغيرات البيئة في الإنتاج (docs/DEPLOYMENT.md).
    /// </summary>
    public static class StartupSettings
    {
        public static void EnsureValid(IConfiguration configuration, bool isDevelopment = false)
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

            // مفاتيح نُشرت في المستودع لا تصلح خارج التطوير (KnownPublicKeys). Security:AllowKnownDevelopmentKeys=true استثناء انتقالي مؤقت.
            if (!isDevelopment && !configuration.GetValue("Security:AllowKnownDevelopmentKeys", false))
            {
                if (KnownPublicKeys.IsPublic(jwtKey))
                    problems.Add("مفتاح توقيع الجلسات (JwtSettings__Key) هو مفتاح التطوير المنشور في المستودع: ولّد مفتاحاً جديداً للإنتاج (docs/DEPLOYMENT.md)");
                if (KnownPublicKeys.IsPublic(configuration["DeviceInventory:PasswordKey"]))
                    problems.Add("مفتاح تشفير الأجهزة (DeviceInventory__PasswordKey) هو مفتاح التطوير المنشور في المستودع: ولّد مفتاحاً جديداً وضع القديم في DeviceInventory__OldPasswordKeys__0 ليُعاد تشفير كلمات السر الحالية (docs/DEPLOYMENT.md)");
            }

            // مجلد المرفقات (FileStorage:Mode=FileSystem): موجود ويقبل الكتابة — مشاركة لم تُركَّب توقف الإقلاع بدل أن تفشل الرفوع لاحقاً
            if (new Infrastructure.Files.FileStorageSettings(configuration).Problem() is string files)
                problems.Add(files);

            // مجلد الأجزاء المؤقتة لرفع وثائق المشاريع (داخل مجلد المرفقات في وضع FileSystem، وإلا Projects:TempFolder أو مجلد مؤقت)
            var storage = new Infrastructure.Files.FileStorageSettings(configuration);
            if (storage.Problem() == null && new Infrastructure.Files.ProjectFileSettings(configuration, storage).Problem() is string uploads)
                problems.Add(uploads);

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
