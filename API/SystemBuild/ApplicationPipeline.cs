using API.Middlewares;
using Microsoft.AspNetCore.HttpOverrides;

namespace API.SystemBuild
{
    public static class ApplicationPipeline
    {
        public static IApplicationBuilder UseApplicationPipeline(this IApplicationBuilder app)
        {
            // 0. خلف Nginx (الإنتاج): البروتوكول وعنوان العميل الأصليان من X-Forwarded-*.
            //    الوكيل الموثوق هو الجهاز نفسه فقط (الافتراضي)، فلا يزوّرها أحد من الخارج. بلا وكيل (التطوير) لا أثر له.
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            // 1. ترويسات الأمان على كل رد (حتى ردود الأخطاء)، ثم معالج الأخطاء
            app.UseMiddleware<SecurityHeadersMiddleware>();
            app.UseMiddleware<ExceptionMiddleware>();

            // 2. CORS، ثم حد المحاولات لكل جهاز (طلبات الدخول — عنوان الجهاز الحقيقي من X-Forwarded-For أعلاه)
            app.UseCors("AllowAngular");
            app.UseRateLimiter();

            // 3. Swagger: فقط إن فُعّل في الإعدادات (Swagger:Enabled — مفعّل في التطوير، معطّل افتراضياً في الإنتاج)
            if (app.ApplicationServices.GetRequiredService<IConfiguration>().GetValue("Swagger:Enabled", false))
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Project Management API");
                    c.DocumentTitle = "Project Management API";
                });
            }

            // 4. HTTPS + Auth
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            return app;
        }
    }
}