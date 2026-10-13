namespace API.Middlewares
{
    /// <summary>
    /// ترويسات الأمان على كل رد (2026-10-13). على السيرفر، Nginx يقدّم ملفات الواجهة بنفسه فيضيف الترويسات نفسها
    /// (docs/DEPLOYMENT.md)؛ وهنا تغطّي ردود الـ API والحزمة الواحدة (الواجهة من wwwroot).
    /// - CSP: سكربتات النظام وحدها (لا سكربت مضمّن)؛ الأنماط المضمّنة مسموحة (أنماط مكوّنات Angular)؛ الصور data: وblob:
    ///   (التوقيعات ومعاينة المرفقات)؛ الإطارات وobject-src: blob: (عارض PDF في Chrome يحتاجها لمعاينة المرفقات)؛ الاتصال بالأصل نفسه (ومعه WebSocket الإشعارات).
    /// - منع عرض النظام داخل إطار، وتخمين نوع الملفات، وإرسال العنوان لمواقع أخرى، والكاميرا والميكروفون والموقع.
    /// - HSTS فقط على HTTPS (لا أثر له قبل تفعيل الشهادة)، بمدة قصيرة تُرفع بعد التأكد.
    /// Swagger (التطوير فقط) بلا CSP لأنه يحتاج سكربتات مضمّنة.
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        public const string ContentSecurityPolicy =
            "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; " +
            "connect-src 'self'; frame-src 'self' blob:; worker-src 'self' blob:; object-src 'self' blob:; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

        public Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "same-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                if (!context.Request.Path.StartsWithSegments("/swagger"))
                    headers["Content-Security-Policy"] = ContentSecurityPolicy;
                if (context.Request.IsHttps)
                    headers["Strict-Transport-Security"] = "max-age=86400";
                return Task.CompletedTask;
            });
            return _next(context);
        }
    }
}
