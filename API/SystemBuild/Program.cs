using API.SystemBuild;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ==================== 1. تسجيل كل الخدمات ====================
builder.Services.AddAPIServices(builder.Configuration);

// ==================== 2. CORS ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("https://localhost:7276") // رابط Blazor
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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