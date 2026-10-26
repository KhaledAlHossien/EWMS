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

            // منفذ أجهزة البصمة المباشر (التطوير: Kestrel على Attendance:DevicePort): لا يخدم إلا /iclock
            var devicePort = app.ApplicationServices.GetRequiredService<Application.Interfaces.IAttendanceSettings>().DevicePort;
            if (devicePort > 0)
                app.Use(async (context, next) =>
                {
                    if (context.Connection.LocalPort == devicePort && !context.Request.Path.StartsWithSegments("/iclock"))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        return;
                    }
                    await next();
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
            // أجهزة البصمة تتصل بـ HTTP على منفذها (لا تُحوَّل إلى HTTPS)
            app.UseWhen(context => !context.Request.Path.StartsWithSegments("/iclock"), branch => branch.UseHttpsRedirection());
            app.UseAuthentication();
            app.UseAuthorization();

            return app;
        }
    }
}