using Application.Interfaces;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Infrastructure
{
    public static class AddInfrastructureRigstrationServices
    {
        public static IServiceCollection AddInfrastructureServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ==================== DbContext ====================
            services.AddDbContext<DataContext>(opt =>
                opt.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // ==================== Services / Repositories ====================
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<IDepartmentService, DepartmentService>();
            services.AddScoped<IOfficeService, OfficeService>();
            services.AddScoped<IWorkTaskService, WorkTaskService>();
            services.AddScoped<IDashboardService, DashboardService>();
            services.AddScoped<IUserPermissionService, UserPermissionService>();
            services.AddScoped<IUserSignatureService, UserSignatureService>();
            services.AddScoped<IAssignedTaskService, AssignedTaskService>();
            services.AddScoped<IMapService, MapService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IRolePermissionService, RolePermissionService>();
            services.AddScoped<IVacationService, VacationService>();
            services.AddScoped<IVacationTypeService, VacationTypeService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<ISiteService, SiteService>();
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<IDeviceSiteService, DeviceSiteService>();
            services.AddScoped<IDeviceInventoryLogService, DeviceInventoryLogService>();
            services.AddSingleton<IDevicePasswordProtector, Infrastructure.Security.DevicePasswordProtector>();
            services.AddSingleton<IDeviceSpreadsheet, Infrastructure.Files.DeviceSpreadsheet>();

            // الصيانة
            services.AddScoped<IDeviceTypeService, DeviceTypeService>();
            services.AddScoped<IDeviceCompanyService, DeviceCompanyService>();
            services.AddScoped<IDamageTypeService, DamageTypeService>();
            services.AddScoped<IPublicHolidayService, PublicHolidayService>();
            services.AddScoped<IMaintenanceRequestStatusService, MaintenanceRequestStatusService>();
            services.AddScoped<IDeviceMaintenanceService, DeviceMaintenanceService>();
            services.AddScoped<IMaintenanceRequestService, MaintenanceRequestService>();
            services.AddScoped<IMaintenanceTaskService, MaintenanceTaskService>();
            services.AddScoped<IToDoListService, ToDoListService>();
            services.AddScoped<ISparePartService, SparePartService>();

            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            // ==================== JWT Services ====================
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthService, AuthService>();

            // ==================== JWT Authentication ====================
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidIssuer = configuration["JwtSettings:Issuer"],
                    ValidAudience = configuration["JwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["JwtSettings:Key"]!))
                };

                options.Events = new JwtBearerEvents
                {
                    // WebSocket (SignalR) لا يستطيع إرسال هيدر Authorization — التوكن يأتي في الـ query
                    // نقبله فقط لمسار الـ hubs، ثم يمر بنفس فحص الإبطال في OnTokenValidated
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },

                    // ✅ التحقق من أن التوكن موجود في قاعدة البيانات ولم يُبطَل
                    OnTokenValidated = async context =>
                    {
                        try
                        {
                            var db = context.HttpContext.RequestServices.GetRequiredService<DataContext>();

                            var tokenString = context.HttpContext.Request.Headers["Authorization"]
                                .ToString()
                                .Replace("Bearer ", "");

                            // اتصال SignalR: التوكن في الـ query (راجع OnMessageReceived)
                            if (string.IsNullOrEmpty(tokenString)
                                && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                            {
                                tokenString = context.HttpContext.Request.Query["access_token"].ToString();
                            }

                            if (string.IsNullOrEmpty(tokenString))
                            {
                                context.Fail("Missing token");
                                return;
                            }

                            var userToken = await db.UserTokens
                                .FirstOrDefaultAsync(t => t.Token == tokenString);

                            if (userToken == null || userToken.IsRevoked || userToken.ExpiresAt < DateTime.UtcNow)
                            {
                                context.Fail("Token revoked or expired");
                            }
                        }
                        catch (Exception ex)
                        {
                            context.Fail($"Token validation error: {ex.Message}");
                        }
                    },

                    // ⚠️ لا نكتب أي شيء هنا — نترك OnChallenge يتولى الرد
                    OnAuthenticationFailed = context =>
                    {
                        // فقط نسجّل الحدث
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");
                        logger.LogWarning("Authentication failed: {Message}", context.Exception?.Message);
                        return Task.CompletedTask;
                    },

                    // ✅ هنا نكتب الرد الوحيد
                    OnChallenge = context =>
                    {
                        context.HandleResponse(); // يمنع ASP.NET من الكتابة الافتراضية

                        if (context.Response.HasStarted)
                            return Task.CompletedTask;

                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";

                        return context.Response.WriteAsync(
                            System.Text.Json.JsonSerializer.Serialize(new
                            {
                                statusCode = 401,
                                message = "Authorization required - please login"
                            }));
                    },

                    // ✅ 403
                    OnForbidden = context =>
                    {
                        if (context.Response.HasStarted)
                            return Task.CompletedTask;

                        context.Response.StatusCode = 403;
                        context.Response.ContentType = "application/json";

                        return context.Response.WriteAsync(
                            System.Text.Json.JsonSerializer.Serialize(new
                            {
                                statusCode = 403,
                                message = "ليس لديك صلاحية لهذا الإجراء"
                            }));
                    }
                };
            });

            return services;
        }
    }
}
