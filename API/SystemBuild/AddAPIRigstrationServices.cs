using API.Authorization;
using API.Hubs;
using Application;
using Application.Common;
using Application.Interfaces;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi;

namespace API.SystemBuild
{
    public static class AddAPIRigstrationServices
    {
        public static IServiceCollection AddAPIServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddControllers();

            // الإشعارات اللحظية (SignalR)
            services.AddSignalR();

            // حد المحاولات لكل جهاز على الدخول وتغيير كلمة المرور (LoginRules) — 429 برسالة عربية
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, token) => new ValueTask(
                    context.HttpContext.Response.WriteAsJsonAsync(new { message = LoginRules.TooManyFromDevice }, token));
                options.AddPolicy(LoginRules.RateLimitPolicy, http => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        PermitLimit = LoginRules.AttemptsPerDevicePerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            });
            services.AddScoped<INotificationPusher, SignalRNotificationPusher>();
            // حماية البيانات: لا ميزة تستعملها اليوم (الجلسات JWT)، لكن ضبطها صريح كي لا تُولَّد مفاتيحها في مجلد مؤقت إن استعملتها ميزة لاحقاً
            // (مضادات التزوير، رموز مؤقتة…). DataProtection:KeysPath = مجلد دائم يقبل الكتابة، وإلا مجلد المستخدم الافتراضي.
            var dataProtection = services.AddDataProtection().SetApplicationName("EWMS");
            if (configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
                dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

            services.AddEndpointsApiExplorer();

            services.AddSwaggerGen(c =>
            {
                // العقد يميّز النص الذي قد يكون null (string?) عن غيره، فتطابق أنواع الواجهة المولَّدة ما يرسله الخادم فعلاً
                c.SupportNonNullableReferenceTypes();
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "EWMS",
                    Version = "v1",
                    Description = "featured API",
                    Contact = new OpenApiContact
                    {
                        Name = "Khaled Alhossien",
                        Email = "hossink131@gmail.com"
                    }
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "Enter: {your token} without {Bearer}",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            });

            services.AddApplicationServices();
            services.AddInfrastructureServices(configuration);
            services.AddHttpContextAccessor();
            services.AddHostedService<API.Background.TaskReminderWorker>();   // تذكيرات مواعيد المهام
            services.AddHostedService<API.Background.ProjectUploadWorker>();   // إنهاء رفع وثائق المشاريع وتنظيف المهجور

            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddAuthorization(options =>
            {
                // سياسة بنفس الاسم لكل صلاحية في AppPermissions (المصدر الوحيد للصلاحيات)
                foreach (var permission in AppPermissions.All.Select(p => p.Name))
                    options.AddPolicy(permission, policy =>
                        policy.Requirements.Add(new PermissionRequirement(permission)));

                // سياسات "أيّ من" (AppPermissions.AnyOf) — منها AnyDeviceView لقراءة توثيق الأجهزة
                foreach (var (name, permissions) in AppPermissions.AnyOf)
                    options.AddPolicy(name, policy => policy.Requirements.Add(new PermissionRequirement(permissions)));
            });

            return services;
        }
    }
}
