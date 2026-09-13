using API.SystemBuild;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==================== 1. تسجيل كل الخدمات ====================
builder.Services.AddAPIServices(builder.Configuration);

// ==================== 2. CORS ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",   // ← Angular
                "https://localhost:4200",  // ← Angular (احتياطًا)
                "https://localhost:7276"   // ← Blazor (إذا ما زلت تستخدمه)
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();   // ← مهم لـ JWT
    });
});

var app = builder.Build();

// ==================== 3. Pipeline ====================
app.UseApplicationPipeline();
app.MapControllers();

// ==================== 4. Migrations + Seeding ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.Run();