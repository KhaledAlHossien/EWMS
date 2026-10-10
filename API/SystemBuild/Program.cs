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
