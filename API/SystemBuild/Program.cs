using API.Hubs;
using API.SystemBuild;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==================== 0. الإعدادات الإلزامية ====================
// الأسرار لا تُكتب في appsettings.json: في التطوير appsettings.Development.json، وفي الإنتاج متغيرات البيئة (docs/DEPLOYMENT.md).
// نقصُ أيٍّ منها يوقف الإقلاع برسالة واضحة بدل أن يفشل لاحقاً عند أول طلب.
StartupSettings.EnsureValid(builder.Configuration);

// ==================== 1. تسجيل كل الخدمات ====================
builder.Services.AddAPIServices(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

// ==================== 2. CORS ====================
// العناوين من الإعدادات Cors:AllowedOrigins (التطوير: localhost:4200). فارغة = الواجهة من نفس عنوان الخادم فقط.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();   // ← مهم لـ JWT
    });
});

var app = builder.Build();

// ==================== 3. Pipeline ====================
app.UseApplicationPipeline();
app.MapControllers();
app.MapHub<NotificationHub>(NotificationHub.Path);
// فحص صحة الخدمة لأداة المراقبة أو موازن الأحمال: 200 Healthy / 503 Unhealthy، بلا تفاصيل داخلية
app.MapHealthChecks("/health");

// ==================== 3.1 الواجهة (Angular) من wwwroot ====================
// حزمة الإصدار تضع ملفات الواجهة في wwwroot: موقع واحد وعنوان واحد (بلا CORS، وSignalR من العنوان نفسه).
// أي مسار ليس ملفاً ولا api/hubs/health/swagger يعيد index.html (مسارات Angular). index.html بلا تخزين مؤقت
// كي تصل النسخة الجديدة فوراً؛ بقية الملفات بأسماء فيها بصمة المحتوى. في التطوير لا يوجد wwwroot/index.html فلا يتغير شيء.
if (app.Environment.WebRootPath is { } webRoot && File.Exists(Path.Combine(webRoot, "index.html")))
{
    var spaFiles = new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
    };
    app.UseDefaultFiles();
    app.UseStaticFiles(spaFiles);
    app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/|hubs/|health|swagger).*$)}", "index.html", spaFiles);
}

// ==================== 4. Migrations + Seeding ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
    // Database:MigrateOnStartup (الافتراضي true): الترحيلات تُطبَّق عند الإقلاع — خذ نسخة احتياطية قبل كل تحديث.
    // false = تُطبَّق يدوياً بسكربت (dotnet ef migrations script) قبل تشغيل النسخة الجديدة.
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, app.Configuration, app.Logger);
    await DbSeeder.BackfillSiteGovernoratesAsync(db);
    await DbSeeder.BackfillVacationSegmentsAsync(db);
    await DbSeeder.EncryptDevicePasswordsAsync(db, scope.ServiceProvider.GetRequiredService<Application.Interfaces.IDevicePasswordProtector>());
}

app.Run();
